using System.Security.Claims;
using Hrms.Core.Services.Approvals;
using Microsoft.AspNetCore.SignalR;

namespace Hrms.Core.Hubs;

public class NotificationHub : Hub
{
    /// <summary>Route the frontend connects to (mapped in Program.cs).</summary>
    public const string Route = "/hubs/notifications";

    // Joins the approver groups this caller's own token entitles it to (see ApproverGroups) so
    // fallback-step approval pushes reach them. Computed from the claims at connect time -- the
    // frontend reconnects after a roles-changed token refresh, and a company switch reloads the
    // page, so the membership never outlives the token it was derived from.
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        var tenantClaim = user?.Claims.FirstOrDefault(c => string.Equals(c.Type, "tenantId", StringComparison.OrdinalIgnoreCase));
        if (user != null && Guid.TryParse(tenantClaim?.Value, out var tenantId) && tenantId != Guid.Empty)
        {
            var isOwnerOrAdmin = user.FindAll(ClaimTypes.Role).Any(c => c.Value is "Owner" or "Admin");
            var permissions = user.FindAll("permission").Select(c => c.Value).ToHashSet();
            foreach (var group in ApproverGroups.GroupsFor(tenantId, isOwnerOrAdmin, permissions))
                await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        await base.OnConnectedAsync();
    }

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