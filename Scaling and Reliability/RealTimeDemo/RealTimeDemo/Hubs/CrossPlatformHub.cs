using Microsoft.AspNetCore.SignalR;

namespace RealTimeDemo.Hubs;

public class CrossPlatformHub : Hub
{
    public async Task ProcessCommand(string command)
    {
        var response = command.ToLower() switch
        {
            "hello" => "Hi there!",
            "time" => DateTime.Now.ToString(),
            "bye" => "Goodbye!",
            _ => $"Unknown command: {command}"
        };

        // Push response to the calling client
        await Clients.Caller.SendAsync("ReceiveMessage", response);
    }
}