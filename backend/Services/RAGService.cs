using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using backend.Models;
using backend.Services;
using ChatResponse = backend.Models.ChatResponse;
using ChatMessage = backend.Models.ChatMessage;
using Document = backend.Models.Document;

namespace backend.Services;

public interface IRAGService
{
    Task<ChatResponse> GenerateResponseAsync(string indexName, string query, string conversationId);
    Task<List<ChatMessage>> GetConversationHistoryAsync(string conversationId);
    Task<int> ProcessDocumentsAsync(string indexName,List<CrawledDocument> documents);
}

public class RAGService : IRAGService
{
    private readonly Kernel _kernel;
    private IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorDatabaseService _vectorDb;
    private readonly DocumentDbContext _dbContext;
    private readonly ILogger<RAGService> _logger;
    private OpenAiOptions _options;
    private readonly IChatCompletionService _chatService;
    private const int ChunkSize = 1000; // Characters per chunk
    private const int ChunkOverlap = 200; // Overlap between chunks

    public RAGService(
        Kernel kernel,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorDatabaseService vectorDb,
        DocumentDbContext dbContext,
        ILogger<RAGService> logger,
        IConfiguration configuration)
    {
        _kernel = kernel;
        _vectorDb = vectorDb;
        _dbContext = dbContext;
        _logger = logger;
        _options = new OpenAiOptions
        {
            ApiKey = configuration["OpenAI:ApiKey"] ?? string.Empty,
            Model = configuration["OpenAI:Model"] ?? string.Empty
        };
        
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        _embeddingGenerator = embeddingGenerator;
    }

    public async Task<ChatResponse> GenerateResponseAsync(string indexName, string query, string conversationId)
    {
        try
        {
            await _vectorDb.CreateIndexIfNotExists(indexName);
            // Generate embedding for the query using Microsoft.Extensions.AI
            var embeddingResult = await _embeddingGenerator.GenerateAsync(query);
            var queryEmbedding = embeddingResult;

            // Retrieve relevant documents from vector database
            var relevantDocs = await _vectorDb.SearchAsync(indexName, queryEmbedding.Vector, 3);

            if (!relevantDocs.Any())
            {
                return new ChatResponse
                {
                    Message = "I don't have any relevant documentation to answer your question. Please crawl documentation websites first using the CRAWL: command.",
                    Success = false
                };
            }

            // Build context from retrieved documents
            var context = BuildContext(relevantDocs);

            // Create prompt with context
            var prompt = BuildRAGPrompt(query, context);

            // Generate response
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are a helpful technical documentation assistant. Answer questions based on the provided documentation context. If the answer isn't in the context, say so clearly.");
            chatHistory.AddUserMessage(prompt);

            var response = await _chatService.GetChatMessageContentAsync(chatHistory);

            // Save to conversation history
            var responseContent = response.Content ?? string.Empty;
            await SaveConversationAsync(conversationId, query, responseContent, relevantDocs);

            return new ChatResponse
            {
                Message = responseContent,
                Sources = relevantDocs.Select(d => new DocumentSource
                {
                    Title = d.Title,
                    Url = d.Url,
                    Snippet = d.Content.Substring(0, Math.Min(200, d.Content.Length))
                }).ToList(),
                Success = true
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

    private string BuildContext(List<RetrievedDocument> documents)
    {
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("=== RELEVANT DOCUMENTATION ===\n");

        for (int i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            contextBuilder.AppendLine($"[Document {i + 1}]");
            contextBuilder.AppendLine($"Title: {doc.Title}");
            contextBuilder.AppendLine($"URL: {doc.Url}");
            contextBuilder.AppendLine($"Content: {doc.Content}");
            contextBuilder.AppendLine($"Relevance Score: {doc.Score:F3}");
            contextBuilder.AppendLine();
        }

        return contextBuilder.ToString();
    }

    private string BuildRAGPrompt(string query, string context)
    {
        return $@"{context}

=== USER QUESTION ===
{query}

=== INSTRUCTIONS ===
Answer the user's question based on the documentation provided above. 
- Cite specific documents when referencing information
- If the answer isn't in the documentation, clearly state that
- Provide code examples if they appear in the documentation
- Be concise but thorough";
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
                    // Generate embedding for chunk
                    var embeddingResult = await _embeddingGenerator.GenerateAsync(chunk);

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

            var overlapText = GetOverlapText(paragraph, ChunkOverlap);
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
            if (currentChunk.Length + paragraph.Length > ChunkSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());

                // Start new chunk with overlap from previous chunk
                var overlapText = GetOverlapText(currentChunk.ToString(), ChunkOverlap);
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

public class OpenAiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public double Temperature { get; set; } = 0.2;
    public int MaxTokens { get; set; } = 500;
    public double TopP { get; set; } = 1.0;
}