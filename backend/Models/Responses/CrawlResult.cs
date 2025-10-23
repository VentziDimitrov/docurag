using backend.Models.DTOs;

namespace backend.Models.Responses;

/// <summary>
/// Result of a web crawling operation
/// </summary>
public record CrawlResult
{
    public bool Success { get; init; }
    public int DocumentsProcessed { get; init; }
    public List<string> ProcessedUrls { get; init; } = new();
    public List<CrawledDocument> Documents { get; init; } = new();
    public string? Error { get; init; }
}
