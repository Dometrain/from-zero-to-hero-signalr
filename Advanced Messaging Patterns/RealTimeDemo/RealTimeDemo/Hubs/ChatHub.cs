using System.Collections.Concurrent;
using System.Reflection.Metadata.Ecma335;
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

    public async Task SendMessage(string message, string user)
    {
        if (message.StartsWith("-COMMAND"))
        {
            message = message.Replace("-COMMAND", "");

            switch (message)
            {
                case "leavegroup1":
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, _lastConnectedId);
                    return;
                case "joingroup1":
                    await Groups.AddToGroupAsync(Context.ConnectionId, "group1");
                    return;
                case "leavegroup2":
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, "group2");
                    return;
                case "groupbroadcastdemo":
                    var groupMessage = "Hello group 1";
                    await Clients.Group("group1").SendAsync("ReceiveMessage", groupMessage);
                    //await Clients.GroupExcept("group1", new string[] {"CONNECTIONID1", "CONNECTIONID2"}).SendAsync("ReceiveMessage", groupMessage);
                    
                    return;
                default:
                    return;
            }
            
        }
        await Clients.All.SendAsync("ReceiveMessage", message, user); 
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"Client connected {Context.ConnectionId}");
        await SendPrivateMessage(Context.ConnectionId, "SERVER: Welcome to the chat");
        await base.OnConnectedAsync();
    }

    public async Task RegisterClient(string groupName, string connectionName)
    {
        await ConnectionState.AddConnectionName(Context.ConnectionId, connectionName);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await ConnectionState.AddConnectionToGroup(Context.ConnectionId, groupName);
        var onlineConnectionNamesInGroup =  await ConnectionState.GetOnlineUsersForGroup(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("UpdateConnectionNames", onlineConnectionNamesInGroup);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task SendToClientConnection(string connectionId, string message)
    {
        await Clients.Client(connectionId).SendAsync("ReceiveMessage", message);
    }

    public async Task SendToUser(string userId, string message)
    {
        await Clients.User(userId).SendAsync("ReceiveMessage", message);
    }

    public async Task SendToGroup(string groupName, string message)
    {
        await Clients.Group(groupName).SendAsync("ReceiveMessage", message);
    }

    public async Task BroadcastToAll(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }

    public async Task BroadcastToAllExceptSender(string message)
    {
        await Clients.Others.SendAsync("ReceiveMessage", message);
    }
    

    public async Task SendPrivateMessage(string connectionId, string message)
    {
        await Clients.Client(connectionId).SendAsync("ReceiveMessage", message);
    }
    
    
   

    public override async Task OnDisconnectedAsync(System.Exception? exception)
    {
        var errorMessage = exception?.Message ?? "No error";

        var groupsToUpdate = await ConnectionState.GetGroupsForConnectionId(Context.ConnectionId);
        await ConnectionState.RemoveConnectionFromAllGroups(Context.ConnectionId);
        await ConnectionState.RemoveConnectionName(Context.ConnectionId);
        
        groupsToUpdate.ForEach(async x =>
        {
            var onlineGroupUsers = await ConnectionState.GetOnlineUsersForGroup(Context.ConnectionId, x);
            await Clients.Group(x).SendAsync("UpdateConnectionNames", onlineGroupUsers);
        });
        
        _logger.LogInformation($"Client disconnected {Context.ConnectionId}. Error {errorMessage}");
        await base.OnDisconnectedAsync(exception);
    }
}

public static class ConnectionState
{
    private static readonly ConcurrentDictionary<string, List<string>> _groups = new();

    private static readonly ConcurrentDictionary<string, string> _connectionNames = new();


    public async static Task<List<string>> GetGroupsForConnectionId(string connectionId)
    {
        var groups = _groups.Where(x 
                => x.Value.Contains(connectionId))
            .Select(z => z.Key).ToList();
            
        return groups;
    }
    
    public async static Task<List<string>> GetOnlineUsersForGroup(string connectionId, string groupName)
    {
        var connectionIds = _groups[groupName];
        var onlineUsers = new List<string>();
        
        foreach(var onlineConnectionId in connectionIds)
        {
            var userName = _connectionNames.ContainsKey(onlineConnectionId)
                ? _connectionNames[onlineConnectionId]
                : "Unknown User";
            onlineUsers.Add(userName);
        }
        
        return onlineUsers;
    }
    
    public async static Task AddConnectionName(string connectionId, string name)
    {
        _connectionNames.GetOrAdd(connectionId, name);
    }

    public async static Task RemoveConnectionName(string connectionId)
    {
        _connectionNames.Remove(connectionId, out var value);
    }

    public async static Task AddConnectionToGroup(string connectionId, string groupName)
    {
        var groupEntry = _groups.GetOrAdd(groupName, new List<string>());
        lock (groupEntry)
        {
            groupEntry.Add(connectionId);
        }
    }

    public async static Task RemoveConnectionFromGroup(string connectionId, string groupName)
    {
        if (_groups.TryGetValue(groupName, out var group))
        {
            lock (group)
            {
                group.Remove(connectionId);
            }
            
        }
    }

    public async static Task RemoveConnectionFromAllGroups(string connectionId)
    {
        var groupsForThisConnection = _groups.Where
        (x =>
            x.Value.Contains(connectionId)).Select(y => y.Key).ToList();

        foreach (var groupName in groupsForThisConnection)
        {
            await RemoveConnectionFromGroup(connectionId, groupName);
        }
    }
}



