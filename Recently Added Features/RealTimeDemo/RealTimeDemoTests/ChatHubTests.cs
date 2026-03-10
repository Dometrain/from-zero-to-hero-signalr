using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RealTimeDemo.Hubs;

namespace RealTimeDemoTests;

public class ChatHubTests
{   
    [Fact]
    public async Task SendMessage_SendToAllClient()
    {
        var mockClients = Substitute.For<IHubCallerClients>();
        var mockClientProxy = Substitute.For<IClientProxy>();
        mockClients.All.Returns(mockClientProxy);
        
        var logger = Substitute.For<ILogger<ChatHub>>();
        
        var chatHub = new ChatHub(logger)
        {
            Clients = mockClients
        };

        var testMessage = "Hello World";

        await chatHub.SendMessage(testMessage);
        
        await mockClientProxy.Received(1).SendCoreAsync("ReceiveMessage", 
            Arg.Is<object[]>(args => (string)args[0] == testMessage), 
            default);
    }
}