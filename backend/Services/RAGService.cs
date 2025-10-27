using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using backend.Models.DTOs;
using backend.Models.Entities;
using backend.Common;
using backend.Agents;
using ChatResponse = backend.Models.Responses.ChatResponse;
using ChatMessage = backend.Models.DTOs.ChatMessage;

namespace backend.Services;

public interface IRAGService
{
    Task<ChatResponse> GenerateResponseAsync(string indexName, string query, string conversationId);
    Task<List<ChatMessage>> GetConversationHistoryAsync(string conversationId);
    Task<int> ProcessDocumentsAsync(string indexName,List<CrawledDocument> documents);
}

public class RAGService : IRAGService
{
    private readonly IAIAgent _aiAgent;
    private readonly IVectorDatabaseService _vectorDb;
    private readonly DocumentDbContext _dbContext;
    private readonly ILogger<RAGService> _logger;

    public RAGService(
        IAIAgent aiAgent,
        IVectorDatabaseService vectorDb,
        DocumentDbContext dbContext,
        ILogger<RAGService> logger)
    {
        _aiAgent = aiAgent;
        _vectorDb = vectorDb;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ChatResponse> GenerateResponseAsync(string indexName, string query, string conversationId)
    {
        try
        {
            await _vectorDb.CreateIndexIfNotExists(indexName);

            // Generate embedding for the query using AIAgent
            var queryEmbedding = await _aiAgent.GenerateEmbeddingAsync(query);

            // Retrieve relevant documents from vector database (get more than needed for reranking)
            var initialTopK = RAGConstants.EnableReRanking
                ? RAGConstants.DefaultTopK * RAGConstants.ReRankMultiplier
                : RAGConstants.DefaultTopK;
            var retrievedDocs = await _vectorDb.SearchAsync(indexName, queryEmbedding.Vector, initialTopK);

            if (retrievedDocs.Count == 0)
            {
                return new ChatResponse
                {
                    Message = "I don't have any relevant documentation to answer your question. Please crawl documentation websites first using the CRAWL: command.",
                    Success = false
                };
            }

            // Re-rank documents using AI Agent for better relevance (if enabled)
            List<RetrievedDocument> reRankedDocs;
            if (RAGConstants.EnableReRanking && retrievedDocs.Count > RAGConstants.DefaultTopK)
            {
                _logger.LogInformation("Re-ranking {Count} documents to top {TopK}", retrievedDocs.Count, RAGConstants.DefaultTopK);
                reRankedDocs = await _aiAgent.ReRankDocumentsAsync(query, retrievedDocs, (int)RAGConstants.DefaultTopK);
            }
            else
            {
                _logger.LogInformation("Using {Count} documents without re-ranking", retrievedDocs.Count);
                reRankedDocs = retrievedDocs.Take((int)RAGConstants.DefaultTopK).ToList();
            }

            // Build context from re-ranked documents using AIAgent
            var context = _aiAgent.BuildContextFromDocuments(reRankedDocs);

            // Create user prompt with context using AIAgent
            var userPrompt = _aiAgent.BuildUserPrompt(query, context);

            // Generate response using AIAgent (system prompt is built internally)
            var responseContent = await _aiAgent.GenerateChatResponseAsync(userPrompt);

            // Assess confidence in the generated response (self-calibration) if enabled
            ConfidenceScore? confidence = null;
            if (RAGConstants.EnableConfidenceAssessment)
            {
                _logger.LogInformation("Assessing response confidence");
                confidence = await _aiAgent.AssessConfidenceAsync(query, responseContent, reRankedDocs);

                if (confidence.Score < RAGConstants.MinConfidenceThreshold)
                {
                    _logger.LogWarning("Low confidence response: {Score:F2}. Reason: {Reasoning}",
                        confidence.Score, confidence.Reasoning);
                }
            }

            // Save to conversation history
            await SaveConversationAsync(conversationId, query, responseContent, reRankedDocs);

            return new ChatResponse
            {
                Message = responseContent,
                Sources = reRankedDocs.Select(d => new backend.Models.Responses.DocumentSource
                {
                    Title = d.Title,
                    Url = d.Url,
                    Snippet = d.Content.Substring(0, Math.Min(RAGConstants.DefaultSnippetLength, d.Content.Length))
                }).ToList(),
                Success = true,
                Confidence = confidence != null ? new backend.Models.Responses.ConfidenceInfo
                {
                    Score = confidence.Score,
                    Reasoning = confidence.Reasoning,
                    MissingInformation = confidence.MissingInformation,
                    IsReliable = confidence.IsReliable
                } : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating RAG response");
            return new ChatResponse
            {
                Message = "An error occurred while generating the response.",
                Success = false
            };
        }
    }

    private async Task SaveConversationAsync(
        string conversationId,
        string query,
        string response,
        List<RetrievedDocument> sources)
    {
        try
        {
            var conversation = new ConversationMessage
            {
                ConversationId = conversationId,
                UserMessage = query,
                AssistantMessage = response,
                Timestamp = DateTime.UtcNow,
                Sources = System.Text.Json.JsonSerializer.Serialize(sources.Select(s => new { s.Url, s.Title }))
            };

            _dbContext.Conversations.Add(conversation);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving conversation");
        }
    }

    public async Task<List<ChatMessage>> GetConversationHistoryAsync(string conversationId)
    {
        try
        {
            var messages = await _dbContext.Conversations
                .Where(c => c.ConversationId == conversationId)
                .OrderBy(c => c.Timestamp)
                .Select(c => new ChatMessage
                {
                    Role = "user",
                    Content = c.UserMessage,
                    Timestamp = c.Timestamp
                })
                .ToListAsync();

            return messages ?? new List<ChatMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history");
            return new List<ChatMessage>();
        }
    }

    public async Task<int> ProcessDocumentsAsync(string indexName, List<CrawledDocument> documents)
    {
        var processedCount = 0;

        await _vectorDb.CreateIndexIfNotExists(indexName);

        foreach (var document in documents)
        {
            try
            {
                // Split document into chunks
                var chunks = SplitIntoParagraphs(document.Content);

                _logger.LogInformation("Processing document: {Title} ({ChunkCount} chunks)",
                    document.Title, chunks.Count);

                foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
                {
                    // Generate embedding for chunk using AIAgent
                    var embeddingResult = await _aiAgent.GenerateEmbeddingAsync(chunk);

                    // Create unique ID for chunk
                    var chunkId = $"{GenerateDocumentId(document.Url)}_{index}";

                    // Prepare metadata
                    var metadata = new Dictionary<string, object>
                                    {
                                        { "title", document.Title },
                                        { "url", document.Url },
                                        { "chunk_index", index },
                                        { "total_chunks", chunks.Count },
                                        { "source_type", "web_crawl" }
                                    };

                    // Add code blocks if present
                    if (document.Metadata.CodeBlocks.Any())
                    {
                        metadata["has_code"] = true;
                    }

                    // Store in vector database
                    await _vectorDb.StoreDocumentAsync(indexName, chunkId, chunk, embeddingResult, metadata);
                }

                // Store document metadata in relational database
                //await StoreDocumentMetadata(document);

                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document: {Url}", document.Url);
            }
        }

        return processedCount;
    }

    private List<string> SplitIntoParagraphs(string text)
    {
        var chunks = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        // Split by paragraphs first
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new System.Text.StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            currentChunk.Append(paragraph.Trim() + "\n\n");

            chunks.Add(currentChunk.ToString());

            var overlapText = GetOverlapText(paragraph, RAGConstants.DefaultChunkOverlap);
            currentChunk.Clear();
            currentChunk.Append(overlapText); 
        }

        // Add remaining content
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks;
    }

    private List<string> SplitIntoChunks(string text)
    {
        var chunks = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        // Split by paragraphs first
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new System.Text.StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            // If adding this paragraph exceeds chunk size
            if (currentChunk.Length + paragraph.Length > RAGConstants.DefaultChunkSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());

                // Start new chunk with overlap from previous chunk
                var overlapText = GetOverlapText(currentChunk.ToString(), RAGConstants.DefaultChunkOverlap);
                currentChunk.Clear();
                currentChunk.Append(overlapText);
            }

            currentChunk.Append(paragraph);
            currentChunk.Append("\n\n");
        }

        // Add remaining content
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks;
    }

    private string GetOverlapText(string text, int overlapSize)
    {
        if (text.Length <= overlapSize)
            return text;

        // Get last 'overlapSize' characters, but try to break at word boundary
        var overlapText = text.Substring(text.Length - overlapSize);
        var firstSpace = overlapText.IndexOf(' ');

        if (firstSpace > 0)
            overlapText = overlapText.Substring(firstSpace + 1);

        return overlapText;
    }

    private string GenerateDocumentId(string url)
    {
        // Create a consistent ID from URL
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hashBytes).Substring(0, 16).ToLower();
    }

    private async Task StoreDocumentMetadata(CrawledDocument document)
    {
        try
        {
            var dbDocument = new Document
            {
                Url = document.Url,
                Title = document.Title,
                CrawledAt = DateTime.UtcNow,
                ContentLength = document.Content.Length,
                Metadata = System.Text.Json.JsonSerializer.Serialize(document.Metadata)
            };

            _dbContext.Documents.Add(dbDocument);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing document metadata");
        }
    }
}