namespace Hrms.Core.Services.Announcements.Lifecycle;

public enum AnnouncementLifecycleWork
{
    Publish,
    Expire,
    Purge,
}

// Delivered at its due time to AnnouncementLifecycleWorker, inside the announcement's company
// (X-Tenant-ID header, resolved by TenantConsumeFilter).
public sealed record AnnouncementLifecycleDue(Guid AnnouncementId, AnnouncementLifecycleWork Work);

/// <summary>
/// Books the time-based lifecycle steps of an announcement -- auto-publish, auto-archive at
/// expiry, purge from the bin -- and cancels them when they no longer apply. Scheduling is part
/// of the same unit of work as the change that caused it (it goes through the bus outbox), so a
/// rolled-back action never leaves a job behind. The worker still re-checks the state when a job
/// fires, so a job that somehow outlived its cancellation is harmless.
/// </summary>
public interface IAnnouncementJobScheduler
{
    /// <returns>The job's token, for <see cref="CancelAsync"/>.</returns>
    Task<Guid> ScheduleAsync(Guid announcementId, AnnouncementLifecycleWork work, DateTime dueUtc, CancellationToken token);

    Task CancelAsync(Guid jobToken, CancellationToken token);
}
