using Microsoft.AspNetCore.SignalR;

namespace Hrms.Core.Services.AI;
/// <summary>
/// SignalR-backed implementation of IAiNotificationService (adapter over
/// IHubContext&lt;MyHub&gt;). The frontend listens for the EventName event.
/// </summary>
public sealed class SignalRAiNotificationService : IAiNotificationService
{
    /// <summary>Client-side event name: connection.on("AiProcessing", ...)</summary>
    public const string EventName = "AiProcessing";

    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRAiNotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyAllAsync(AiProcessingNotification notification, CancellationToken cancellationToken = default)
        => _hubContext.Clients.All.SendAsync(EventName, notification, cancellationToken);

    public Task NotifyGroupAsync(string groupName, AiProcessingNotification notification, CancellationToken cancellationToken = default)
        => _hubContext.Clients.Group(groupName).SendAsync(EventName, notification, cancellationToken);
}
