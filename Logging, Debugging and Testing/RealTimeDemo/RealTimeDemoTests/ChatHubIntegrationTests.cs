
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using RealTimeDemo;

namespace RealTimeDemoTests;

public class ChatHubIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _webApplicationFactory;

    public ChatHubIntegrationTests(WebApplicationFactory<Program> webApplicationFactory)
    {
        _webApplicationFactory = webApplicationFactory;
    }
    
    [Fact]
    public async Task ClientReceivesMessage_FromServer()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => _webApplicationFactory.Server.CreateHandler();
            })
            .Build();
        
        string? receivedMessage = null;
        var testMessage = "Hello world!";
        connection.On<string>("ReceiveMessage", (message) =>
        {
            receivedMessage = message;
        });
        
        await connection.StartAsync();
        try
        {
            await connection.InvokeAsync("SendMessage", testMessage);
            await Task.Delay(300);
        }
        catch (Exception ex)
        {
            throw;
        }
       
        
        Assert.Equal(testMessage, receivedMessage);
    }
}