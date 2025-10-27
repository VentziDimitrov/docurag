using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.AI;
using backend.Models.DTOs;
using System.Text.Json;

namespace backend.Agents;

public interface IAIAgent
{
    Task<Embedding<float>> GenerateEmbeddingAsync(string text);
    Task<string> GenerateChatResponseAsync(string userPrompt);
    Task<List<RetrievedDocument>> ReRankDocumentsAsync(string query, List<RetrievedDocument> documents, int topK);
    Task<ConfidenceScore> AssessConfidenceAsync(string query, string response, List<RetrievedDocument> documents);
    string BuildContextFromDocuments(List<RetrievedDocument> documents);
    string BuildSystemPrompt();
    string BuildUserPrompt(string query, string context);
}

public record ConfidenceScore
{
    public float Score { get; init; } // 0.0 to 1.0
    public string Reasoning { get; init; } = string.Empty;
    public List<string> MissingInformation { get; init; } = [];
    public bool IsReliable { get; init; }
}

public record DocumentRelevanceScore
{
    public string DocumentId { get; init; } = string.Empty;
    public float RelevanceScore { get; init; }
    public string Reasoning { get; init; } = string.Empty;
}

public class AIAgent : IAIAgent
{
    private readonly Kernel _kernel;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<AIAgent> _logger;

    public AIAgent(
        Kernel kernel,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<AIAgent> logger)
    {
        _kernel = kernel;
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<Embedding<float>> GenerateEmbeddingAsync(string text)
    {
        try
        {
            _logger.LogDebug("Generating embedding for text of length: {Length}", text.Length);
            var embeddingResult = await _embeddingGenerator.GenerateAsync(text);
            return embeddingResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
            throw;
        }
    }

    public async Task<string> GenerateChatResponseAsync(string userPrompt)
    {
        try
        {
            _logger.LogDebug("Generating chat response");

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(BuildSystemPrompt());
            chatHistory.AddUserMessage(userPrompt);

            var response = await _chatService.GetChatMessageContentAsync(chatHistory);

            return response.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating chat response");
            throw;
        }
    }

    public string BuildContextFromDocuments(List<RetrievedDocument> documents)
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

    public string BuildSystemPrompt()
    {
        return @"You are an expert technical documentation assistant with deep knowledge of software development, APIs, frameworks, and programming languages.

Your role is to:
1. Analyze and understand technical documentation thoroughly
2. Provide accurate, precise answers based solely on the provided documentation context
3. Explain complex technical concepts in a clear, accessible manner
4. Include relevant code examples and usage patterns when available
5. Cite sources by referencing specific documents when providing information
6. Be honest about limitations - if information isn't in the documentation, clearly state that

Guidelines for responses:
- ACCURACY: Only use information from the provided documentation. Never make assumptions or add information not present in the context.
- CITATIONS: Always reference which document(s) you're drawing information from (e.g., ""According to Document 1..."" or ""As shown in the [Title]..."")
- CODE EXAMPLES: When code snippets are available in the documentation, include them with proper formatting
- CLARITY: Break down complex topics into digestible explanations
- COMPLETENESS: Provide thorough answers but remain concise
- HONESTY: If the documentation doesn't contain the answer, say so explicitly and suggest what information might be needed

Format your responses with:
- Clear structure using headings when appropriate
- Code blocks with language specification for syntax highlighting
- Bullet points or numbered lists for multiple items
- Links to source documents when referencing specific information";
    }

    public string BuildUserPrompt(string query, string context)
    {
        return $@"I have retrieved the following relevant documentation to answer your question:

{context}

Based on the documentation above, please answer this question:
{query}

Remember to:
- Only use information from the provided documentation
- Cite which document(s) you're referencing
- Include code examples if they appear in the documentation
- If the answer isn't in the documentation, clearly state that
- Provide a thorough but concise response";
    }

    public async Task<List<RetrievedDocument>> ReRankDocumentsAsync(string query, List<RetrievedDocument> documents, int topK)
    {
        try
        {
            _logger.LogInformation("Re-ranking {Count} documents for query", documents.Count);

            if (documents.Count <= topK)
            {
                _logger.LogDebug("Document count ({Count}) is less than or equal to topK ({TopK}), returning all documents", documents.Count, topK);
                return documents;
            }

            // Build reranking prompt
            var reRankingPrompt = BuildReRankingPrompt(query, documents);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(BuildReRankingSystemPrompt());
            chatHistory.AddUserMessage(reRankingPrompt);

            var response = await _chatService.GetChatMessageContentAsync(chatHistory);
            var responseContent = response.Content ?? string.Empty;

            // Parse the response to extract document rankings
            var rankings = ParseReRankingResponse(responseContent, documents);

            // Sort documents by relevance score and take top K
            var reRankedDocuments = rankings
                .OrderByDescending(r => r.RelevanceScore)
                .Take(topK)
                .Select(r => documents.First(d => d.Id == r.DocumentId))
                .ToList();

            _logger.LogInformation("Re-ranked documents. Top document: {Title} (Score: {Score})",
                reRankedDocuments.First().Title, rankings.First().RelevanceScore);

            return reRankedDocuments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-ranking documents. Returning original list with topK limit");
            return documents.Take(topK).ToList();
        }
    }

    public async Task<ConfidenceScore> AssessConfidenceAsync(string query, string response, List<RetrievedDocument> documents)
    {
        try
        {
            _logger.LogDebug("Assessing confidence for response");

            var confidencePrompt = BuildConfidenceAssessmentPrompt(query, response, documents);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(BuildConfidenceSystemPrompt());
            chatHistory.AddUserMessage(confidencePrompt);

            var aiResponse = await _chatService.GetChatMessageContentAsync(chatHistory);
            var responseContent = aiResponse.Content ?? string.Empty;

            // Parse the confidence assessment
            var confidenceScore = ParseConfidenceResponse(responseContent);

            _logger.LogInformation("Confidence assessment: {Score:F2} - {Reasoning}",
                confidenceScore.Score, confidenceScore.Reasoning);

            return confidenceScore;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing confidence");
            return new ConfidenceScore
            {
                Score = 0.5f,
                Reasoning = "Unable to assess confidence due to an error",
                IsReliable = false
            };
        }
    }

    private string BuildReRankingSystemPrompt()
    {
        return @"You are a document relevance assessment expert. Your task is to evaluate how relevant each document is to answering a specific user query.

Analyze each document carefully and assign a relevance score from 0.0 to 1.0, where:
- 1.0: Highly relevant, directly answers the query
- 0.7-0.9: Very relevant, contains important related information
- 0.4-0.6: Moderately relevant, has some related information
- 0.1-0.3: Slightly relevant, mentions related topics
- 0.0: Not relevant at all

You must respond with a valid JSON array containing objects with documentId, relevanceScore, and reasoning fields.";
    }

    private string BuildReRankingPrompt(string query, List<RetrievedDocument> documents)
    {
        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.AppendLine($"Query: {query}\n");
        promptBuilder.AppendLine("Documents to evaluate:\n");

        for (int i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            promptBuilder.AppendLine($"Document ID: {doc.Id}");
            promptBuilder.AppendLine($"Title: {doc.Title}");
            promptBuilder.AppendLine($"Content Preview: {doc.Content.Substring(0, Math.Min(500, doc.Content.Length))}...");
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("Provide your assessment as a JSON array with this format:");
        promptBuilder.AppendLine("[{\"documentId\": \"doc_id\", \"relevanceScore\": 0.95, \"reasoning\": \"explanation\"}]");

        return promptBuilder.ToString();
    }

    private string BuildConfidenceSystemPrompt()
    {
        return @"You are a self-calibration expert that assesses the quality and reliability of AI-generated responses.

Your task is to evaluate:
1. How well the response answers the user's query
2. Whether the response is fully supported by the provided documentation
3. What information (if any) is missing or incomplete
4. The overall reliability of the response

Provide your assessment as a JSON object with:
- score: float (0.0 to 1.0) representing confidence level
- reasoning: string explaining your assessment
- missingInformation: array of strings describing what's missing
- isReliable: boolean indicating if the response can be trusted

Be critical and honest in your assessment.";
    }

    private string BuildConfidenceAssessmentPrompt(string query, string response, List<RetrievedDocument> documents)
    {
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("=== ORIGINAL QUERY ===");
        contextBuilder.AppendLine(query);
        contextBuilder.AppendLine();

        contextBuilder.AppendLine("=== AI RESPONSE ===");
        contextBuilder.AppendLine(response);
        contextBuilder.AppendLine();

        contextBuilder.AppendLine("=== SOURCE DOCUMENTS ===");
        for (int i = 0; i < documents.Count; i++)
        {
            contextBuilder.AppendLine($"[Document {i + 1}: {documents[i].Title}]");
            contextBuilder.AppendLine(documents[i].Content.Substring(0, Math.Min(300, documents[i].Content.Length)) + "...");
            contextBuilder.AppendLine();
        }

        contextBuilder.AppendLine("\nProvide your confidence assessment as a JSON object:");
        contextBuilder.AppendLine("{\"score\": 0.85, \"reasoning\": \"...\", \"missingInformation\": [...], \"isReliable\": true}");

        return contextBuilder.ToString();
    }

    private List<DocumentRelevanceScore> ParseReRankingResponse(string response, List<RetrievedDocument> documents)
    {
        try
        {
            // Extract JSON from response (handle cases where AI adds explanation text)
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var rankings = JsonSerializer.Deserialize<List<DocumentRelevanceScore>>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (rankings != null && rankings.Any())
                {
                    return rankings;
                }
            }

            _logger.LogWarning("Failed to parse reranking response, using original scores");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing reranking response");
        }

        // Fallback: return original scores
        return documents.Select(d => new DocumentRelevanceScore
        {
            DocumentId = d.Id,
            RelevanceScore = d.Score,
            Reasoning = "Original vector search score"
        }).ToList();
    }

    private ConfidenceScore ParseConfidenceResponse(string response)
    {
        try
        {
            // Extract JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var confidence = JsonSerializer.Deserialize<ConfidenceScore>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (confidence != null)
                {
                    return confidence;
                }
            }

            _logger.LogWarning("Failed to parse confidence response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing confidence response");
        }

        // Fallback
        return new ConfidenceScore
        {
            Score = 0.5f,
            Reasoning = "Unable to parse confidence assessment",
            IsReliable = false
        };
    }
}
