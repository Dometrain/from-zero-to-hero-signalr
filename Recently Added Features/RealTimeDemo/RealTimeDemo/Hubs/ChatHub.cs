using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RealTimeDemo.Models;

namespace RealTimeDemo.Hubs;

public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;
    private string _lastConnectedId = "";
    private ActivitySource _activitySource = new ActivitySource("RealTimeDemo");
    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    public async Task SendMessage(string message)
    {
        using var activity = _activitySource.StartActivity("SendMessage");
        activity?.AddTag("hub", "chathub");
        activity?.AddTag("connectionId", Context.ConnectionId);
        
        var rateLimitingSeconds = 1;
  
        if (ConnectionState.LastMessageTime.TryGetValue(Context.UserIdentifier, out var lastMessageTime)
            && (DateTime.UtcNow - lastMessageTime).TotalSeconds < rateLimitingSeconds)
        {
            throw new HubException("Rate limit reached. Try again later");
        }
        
        ConnectionState.LastMessageTime[Context.UserIdentifier] = DateTime.UtcNow;
       
        if (message.StartsWith("-COMMAND"))
        {
            message = message.Replace("-COMMAND", "");

            switch (message)
            {
                case "totalsalesusers":
                    var totalSalesUsers = ConnectionState.OnlineUsers.
                        Where(x => x.IsInRole("Sales")).Count();
                    await Clients.Caller.SendAsync("ReceiveMessage", $"COMMAND RESPONSE: {totalSalesUsers} ONLINE" );
                    return;
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
        _logger.LogDebug("Message being sent");
        await Clients.All.SendAsync("ReceiveMessage", message); 
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"Client connected {Context.ConnectionId}");
        await SendPrivateMessage(Context.ConnectionId, "SERVER: Welcome to the chat");
        await base.OnConnectedAsync();
    }
    
  
    public async Task RegisterClient(string groupName)
    {
        if (Context.User.IsInRole(groupName))
        {
            if (ConnectionState.IsValidUser(Context.UserIdentifier, groupName))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                ConnectionState.ToggleUserOnlineState(Context.UserIdentifier, true);
                var onlineConnectionNamesInGroup =  await ConnectionState.GetOnlineUsersForGroup(groupName);
                var offlineUsersForGroup = await ConnectionState.GetOfflineUsersForGroup(groupName);
                await Clients.Group(groupName).SendAsync("UpdateConnectionNames", onlineConnectionNamesInGroup, offlineUsersForGroup);
            }
            //ConnectionState.OnlineUsers.Add(Context.User);
            //await ConnectionState.AddConnectionName(Context.ConnectionId, connectionName);
          
            //await ConnectionState.AddConnectionToGroup(Context.ConnectionId, groupName);
           
        }
        else
        {
            _logger.LogError($"User is not in group {groupName}");
            throw new UnauthorizedAccessException();
        }
        
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
        _logger.LogDebug($"Broadcasting to all from {Context.ConnectionId}");
        await Clients.All.SendAsync("ReceiveMessage", message);
        _logger.LogDebug("Broadcast to all");
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
        if (Context.UserIdentifier != null)
        {
            var user = ConnectionState.GetUserByUserName(Context.UserIdentifier);
            ConnectionState.ToggleUserOnlineState(Context.UserIdentifier, false);
      
            user.Groups.ForEach(async x =>
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, x);
                var onlineUsersForGroup = await ConnectionState.GetOnlineUsersForGroup(x);
                var offlineUsersForGroup = await ConnectionState.GetOfflineUsersForGroup(x);
                await Clients.Group(x).SendAsync("UpdateConnectionNames", onlineUsersForGroup, offlineUsersForGroup);
            });
        }
        _logger.LogWarning($"Client disconnected {Context.ConnectionId}. Error {errorMessage}");
        await base.OnDisconnectedAsync(exception);
    }
}

public static class ConnectionState
{
    
    private static ConcurrentDictionary<string, User> _users = new ConcurrentDictionary<string, User>();
    public static ConcurrentDictionary<string, DateTime> LastMessageTime = new();
    static ConnectionState()
    {
        var user1 = new User()
        {
            Name = "nick",
            IsOnline = false,
            Groups = new List<string>() { "Sales" }
        };
        
        var user2 = new User()
        {
            Name = "nick-pro",
            IsOnline = false,
            Groups = new List<string>() { "Sales" }
        };

        _users.TryAdd("nick", user1);
        _users.TryAdd("nick-pro", user2);
    }

    public static void ToggleUserOnlineState(string userName, bool isOnline)
    {
        if (_users.ContainsKey(userName) == false)
        {
            throw new ArgumentException("User not found");
        }
        
        var user = _users[userName];
        user.IsOnline = isOnline;
    }

    public static User GetUserByUserName(string userName)
    {
        if (_users.ContainsKey(userName) == false)
        {
            throw new ArgumentException("User not found");
        }
        return _users[userName];
    }
    
    public static bool IsValidUser(string userName, string groupname)
    {
        if (_users.ContainsKey(userName) == false)
        {
            return false;
        }
        
        var user = _users[userName];
        return user.Groups.Contains(groupname);
    }
    
    
    private static readonly ConcurrentDictionary<string, List<string>> _groups = new();

    private static readonly ConcurrentDictionary<string, string> _connectionNames = new();
    
    public static ConcurrentBag<ClaimsPrincipal> OnlineUsers { get; } = new();
    
    /*public async static Task<List<string>> GetGroupsForConnectionId(string connectionId)
    {
        var groups = _groups.Where(x 
                => x.Value.Contains(connectionId))
            .Select(z => z.Key).ToList();
            
        return groups;
    }*/
    
    public async static Task<List<string>> GetOnlineUsersForGroup(string groupName)
    {
        
        var onlineUsers = _users.Where(x => 
            x.Value.IsOnline)
            .Select(x => x.Key)
            .ToList();
        
        return onlineUsers;
    }
    
    public async static Task<List<string>> GetOfflineUsersForGroup(string groupName)
    {
        
        var onlineUsers = _users.Where(x => 
                x.Value.IsOnline == false)
            .Select(x => x.Key)
            .ToList();
        
        return onlineUsers;
    }
    
    /*public async static Task AddConnectionName(string connectionId, string name)
    {
        _connectionNames.GetOrAdd(connectionId, name);
    }*/

    /*public async static Task RemoveConnectionName(string connectionId)
    {
        _connectionNames.Remove(connectionId, out var value);
    }*/

    /*public async static Task AddConnectionToGroup(string connectionId, string groupName)
    {
        var groupEntry = _groups.GetOrAdd(groupName, new List<string>());
        lock (groupEntry)
        {
            groupEntry.Add(connectionId);
        }
    }*/

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



