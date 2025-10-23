namespace backend.Models.DTOs;

/// <summary>
/// Represents a chat message in a conversation
/// </summary>
public record ChatMessage
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
