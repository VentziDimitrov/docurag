namespace backend.Models.Entities;

/// <summary>
/// Database entity for conversation messages
/// </summary>
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
