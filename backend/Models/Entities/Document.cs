namespace backend.Models.Entities;

/// <summary>
/// Database entity for documents
/// </summary>
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
