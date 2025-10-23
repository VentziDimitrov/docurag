using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

public class CrawlerHub : Hub
{
    private readonly ILogger<CrawlerHub> _logger;

    public CrawlerHub(ILogger<CrawlerHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task RegisterConnection(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        _logger.LogInformation("User {UserId} registered with connection {ConnectionId}", 
            userId, Context.ConnectionId);
    }
}