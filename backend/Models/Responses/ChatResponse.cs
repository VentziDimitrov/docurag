namespace backend.Models.Responses;

/// <summary>
/// Response model for chat messages
/// </summary>
public record ChatResponse
{
    public string Message { get; init; } = string.Empty;
    public List<DocumentSource> Sources { get; init; } = [];
    public bool Success { get; init; }
    public ConfidenceInfo? Confidence { get; init; }
}

/// <summary>
/// Confidence information for the response
/// </summary>
public record ConfidenceInfo
{
    public float Score { get; init; }
    public string Reasoning { get; init; } = string.Empty;
    public List<string> MissingInformation { get; init; } = [];
    public bool IsReliable { get; init; }
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
