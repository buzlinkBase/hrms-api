using Hrms.Core.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Hrms.Core.Services.Approvals;

// Adapter over IHubContext<NotificationHub>, mirroring SignalRAiNotificationService's existing
// pattern (Hrms.Core\Services\AI\SignalRAiNotificationService.cs) -- same hub, same "push via
// IHubContext" shape, just per-user targeting instead of per-group, via Clients.User(userId)
// (enabled app-wide by HrmsHubUserIdProvider).
public sealed class ApprovalPushNotificationService
{
    /// <summary>Client-side event name: connection.on("ApprovalNotification", ...)</summary>
    public const string EventName = "ApprovalNotification";

    private readonly IHubContext<NotificationHub> _hubContext;

    public ApprovalPushNotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PushToUserAsync(Guid userId, ApprovalPushNotification notification, CancellationToken token = default) =>
        _hubContext.Clients.User(userId.ToString()).SendAsync(EventName, notification, token);

    // Everyone currently connected who may approve this type in this tenant -- the fallback-step
    // audience, see ApproverGroups.
    public Task PushToApproversAsync(Guid tenantId, ApprovalPushNotification notification, CancellationToken token = default) =>
        _hubContext.Clients.Group(ApproverGroups.For(tenantId, notification.ApplicationType)).SendAsync(EventName, notification, token);
}
