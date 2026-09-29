using Hrms.Core.Hubs;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities.Announcements;
using Microsoft.AspNetCore.SignalR;

namespace Hrms.Core.Services.Announcements;

// Live bell update for announcement recipients, on the same NotificationHub the approval pushes
// use (see ApprovalPushNotificationService). A person is addressed by company + login email
// first -- the per-person group every connection joins (ApprovalRecipientGroups) -- and by
// UserId otherwise. Always called after the change is committed: a failed push only means the
// bell catches up on its next poll, never a rolled-back publish.
public sealed class AnnouncementPushService
{
    /// <summary>Client-side event name: connection.on("AnnouncementNotification", ...)</summary>
    public const string EventName = "AnnouncementNotification";

    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ITenantProvider _tenantProvider;

    public AnnouncementPushService(IHubContext<NotificationHub> hubContext, ITenantProvider tenantProvider)
    {
        _hubContext = hubContext;
        _tenantProvider = tenantProvider;
    }

    public async Task PushAsync(IEnumerable<AnnouncementRecipient> recipients, AnnouncementPushNotification notification, CancellationToken token = default)
    {
        var tenantId = _tenantProvider.TenantId;
        var groups = new List<string>();
        var userIds = new List<string>();
        foreach (var recipient in recipients)
        {
            if (!string.IsNullOrWhiteSpace(recipient.Email) && tenantId != Guid.Empty)
                groups.Add(ApprovalRecipientGroups.For(tenantId, recipient.Email));
            else if (recipient.UserId is { } userId)
                userIds.Add(userId.ToString());
        }

        if (groups.Count > 0)
            await _hubContext.Clients.Groups(groups.Distinct().ToList()).SendAsync(EventName, notification, token);
        if (userIds.Count > 0)
            await _hubContext.Clients.Users(userIds.Distinct().ToList()).SendAsync(EventName, notification, token);
    }
}
