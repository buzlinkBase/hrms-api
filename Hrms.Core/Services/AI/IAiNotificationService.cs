namespace Hrms.Core.Services.AI;

/// <summary>
/// Pushes AI processing events to the frontend. AI code depends on this
/// interface, not on SignalR, so the transport can change without touching
/// callers.
/// </summary>
public interface IAiNotificationService
{
    /// <summary>Notifies every connected client.</summary>
    Task NotifyAllAsync(AiProcessingNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies only clients that joined the group (see AiGroups.ForJob /
    /// MyHub.JoinGroup), so each user receives only their own job's events.
    /// </summary>
    Task NotifyGroupAsync(string groupName, AiProcessingNotification notification, CancellationToken cancellationToken = default);
}
