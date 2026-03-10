using Microsoft.AspNetCore.SignalR;

namespace RealTimeDemo.Hubs;

public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;
    private string _lastConnectedId = "";
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
        await SendPrivateMessage(Context.ConnectionId, "SERVER: Welcome to the chat");
        
        await base.OnConnectedAsync();
    }

    public async Task SendPrivateMessage(string connectionId, string message)
    {
        await Clients.Client(connectionId).SendAsync("ReceiveMessage", message);
    }
    
   

    public override async Task OnDisconnectedAsync(System.Exception? exception)
    {
        var errorMessage = exception?.Message ?? "No error";
        _logger.LogInformation($"Client disconnected {Context.ConnectionId}. Error {errorMessage}");
        await base.OnDisconnectedAsync(exception);
    }
}