namespace backend.Models.Responses;

/// <summary>
/// Response model for chat messages
/// </summary>
public record ChatResponse
{
    public string Message { get; init; } = string.Empty;
    public List<DocumentSource> Sources { get; init; } = new();
    public bool Success { get; init; }
}

/// <summary>
/// Represents a source document in a chat response
/// </summary>
public record DocumentSource
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Snippet { get; init; } = string.Empty;
}
