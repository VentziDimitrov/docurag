using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.AI;
using backend.Models.DTOs;

namespace backend.Agents;

public interface IAIAgent
{
    Task<Embedding<float>> GenerateEmbeddingAsync(string text);
    Task<string> GenerateChatResponseAsync(string userPrompt, string context);
    string BuildContextFromDocuments(List<RetrievedDocument> documents);
    string BuildSystemPrompt();
    string BuildUserPrompt(string query, string context);
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

    public async Task<string> GenerateChatResponseAsync(string userPrompt, string context)
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
}
