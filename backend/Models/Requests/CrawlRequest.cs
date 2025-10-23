using System.ComponentModel.DataAnnotations;

namespace backend.Models.Requests;

/// <summary>
/// Request model for web crawling operations
/// </summary>
public record CrawlRequest
{
    [Required(ErrorMessage = "IndexName is required")]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "IndexName must contain only lowercase letters, numbers, and hyphens")]
    public string IndexName { get; init; } = string.Empty;

    [Required(ErrorMessage = "URL is required")]
    [Url(ErrorMessage = "Must be a valid URL")]
    public string Url { get; init; } = string.Empty;

    [Required(ErrorMessage = "ConnectionId is required")]
    public string ConnectionId { get; init; } = string.Empty;
}
