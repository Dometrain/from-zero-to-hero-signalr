using Microsoft.AspNetCore.SignalR;

namespace RealTimeDemo.Hubs;

public class StronglyTypedChatHub : Hub<IStronglyTypedChatHub>
{
    private readonly ILogger<IStronglyTypedChatHub> _logger;

    public StronglyTypedChatHub(ILogger<IStronglyTypedChatHub> logger)
    {
        _logger = logger;
    }

    public async Task SendMessage(string message, string user)
    {
        await Clients.All.ReceiveMessage(message, user);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"Client connected {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(System.Exception? exception)
    {
        var errorMessage = exception?.Message ?? "No error";
        _logger.LogInformation($"Client disconnected {Context.ConnectionId}. Error {errorMessage}");
        await base.OnDisconnectedAsync(exception);
    }
}

public interface IStronglyTypedChatHub
{
    Task ReceiveMessage(string message, string user);
}