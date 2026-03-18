using Microsoft.AspNetCore.SignalR.Client;

namespace Client;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting SignalR client....");
        var url = "http://localhost:5276/hubs/crossplatform";

        var connection = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .Build();

        connection.On<string>("ReceiveMessage", (message) =>
        {
            Console.WriteLine(message);
        });

        try
        {
            await connection.StartAsync();
            Console.WriteLine("Connected to SignalR hub");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
            return;
        }

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            try
            {
                await connection.SendAsync("ProcessCommand", input);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }
        
        await connection.StopAsync();
        Console.WriteLine("Disconnected from SignalR hub");

    }
}