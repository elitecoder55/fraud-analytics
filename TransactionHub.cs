using FraudAnalytics.Models;
using Microsoft.AspNetCore.SignalR;

namespace FraudAnalytics.Hubs;

/// <summary>
/// SignalR hub — clients subscribe and receive live transaction + alert pushes.
/// The backend calls IHubContext&lt;TransactionHub&gt; from services to broadcast.
/// </summary>
public class TransactionHub : Hub
{
    private readonly ILogger<TransactionHub> _logger;

    public TransactionHub(ILogger<TransactionHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {Id}", Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "analysts");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {Id}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Analysts can subscribe to only HIGH-RISK updates
    public async Task SubscribeHighRisk()
        => await Groups.AddToGroupAsync(Context.ConnectionId, "high-risk-only");

    public async Task UnsubscribeHighRisk()
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, "high-risk-only");
}
