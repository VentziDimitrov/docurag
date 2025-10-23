namespace backend.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
}

public class CrawlRequest
{
    public string IndexName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public List<DocumentSource> Sources { get; set; } = new();
    public bool Success { get; set; }
}

public class DocumentSource
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class CrawledDocument
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    [JsonPropertyName("metadata")]
    public CrawledMetadata Metadata { get; set; } = new();
}

public class CrawledMetadata
{
    [JsonPropertyName("code_blocks")]
    public List<string> CodeBlocks { get; set; } = new List<string>();

    [JsonPropertyName("depth")]
    public int Depth { get; set; }
    [JsonPropertyName("crawled_at")]
    public string CrawledAt { get; set; } = string.Empty;
}

public class RetrievedDocument
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public float Score { get; set; }
}

public class ConversationMessage
{
    public int Id { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string DocumentationName { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public string AssistantMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Sources { get; set; }
}

public class Document
{
    public int Id { get; set; }
    public string DocumentationName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CrawledAt { get; set; }
    public int ContentLength { get; set; }
    public string? Metadata { get; set; }
}