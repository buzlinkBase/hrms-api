using Microsoft.AspNetCore.SignalR;

namespace Hrms.Core.Hubs;

public class NotificationHub : Hub
{
    /// <summary>Route the frontend connects to (mapped in Program.cs).</summary>
    public const string Route = "/hubs/notifications";

    /// <summary>
    /// Clients join a group to receive targeted notifications, e.g. the
    /// AI job group returned by AiGroups.ForJob(jobId).
    /// </summary>
    public Task JoinGroup(string groupName)
        => Groups.AddToGroupAsync(Context.ConnectionId, groupName);

    public Task LeaveGroup(string groupName)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

    public async Task SendMessage(string user, string message)
    {
        // "ReceiveMessage" is the event name everyone will listen to
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}