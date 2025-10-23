using System.Text.Json.Serialization;

namespace backend.Models.DTOs;

/// <summary>
/// Represents a document crawled from a website
/// </summary>
public record CrawledDocument
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("metadata")]
    public CrawledMetadata Metadata { get; init; } = new();
}

/// <summary>
/// Metadata for a crawled document
/// </summary>
public record CrawledMetadata
{
    [JsonPropertyName("code_blocks")]
    public List<string> CodeBlocks { get; init; } = new();

    [JsonPropertyName("depth")]
    public int Depth { get; init; }

    [JsonPropertyName("crawled_at")]
    public string CrawledAt { get; init; } = string.Empty;
}
