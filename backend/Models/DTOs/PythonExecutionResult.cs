using System.Text.Json.Serialization;

namespace backend.Models.DTOs;

/// <summary>
/// Result of executing a Python script
/// </summary>
public record PythonExecutionResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public CrawlerOutput Output { get; init; } = new();
    public string Error { get; init; } = string.Empty;
}

/// <summary>
/// Output from the web crawler Python script
/// </summary>
public record CrawlerOutput
{
    [JsonPropertyName("docs")]
    public List<CrawledDocument> Documents { get; init; } = new();

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}
