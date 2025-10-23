namespace backend.Models.DTOs;

/// <summary>
/// Status update for crawling operations
/// </summary>
public record CrawlStatus
{
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int Progress { get; init; }
}
