using Microsoft.AspNetCore.SignalR;

namespace RealTimeDemo.Hubs;

public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    public async Task SendMessage(string messsage, string user)
    {
       await Clients.All.SendAsync("ReceiveMessage", messsage, user); 
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