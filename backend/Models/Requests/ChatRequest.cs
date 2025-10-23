namespace backend.Models.Requests;

/// <summary>
/// Request model for chat messages
/// </summary>
public record ChatRequest(
    string Message,
    string ConversationId,
    string ConnectionId
);
