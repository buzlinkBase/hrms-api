using Hrms.Core.Messaging.Filter;
using MassTransit;

namespace Hrms.Core.Services.Announcements.Lifecycle;

/// <summary>
/// <see cref="IAnnouncementJobScheduler"/> on MassTransit's message scheduler, backed by Quartz
/// with a persistent, clustered job store (see RabbitMqConfiguration). A job survives restarts
/// and broker loss, and firing it publishes <see cref="AnnouncementLifecycleDue"/> carrying the
/// company it was booked for -- so companies on a dedicated database are covered too.
/// </summary>
public sealed class QuartzAnnouncementJobScheduler : IAnnouncementJobScheduler
{
    private readonly IMessageScheduler _scheduler;
    private readonly ITenantProvider _tenantProvider;

    public QuartzAnnouncementJobScheduler(IMessageScheduler scheduler, ITenantProvider tenantProvider)
    {
        _scheduler = scheduler;
        _tenantProvider = tenantProvider;
    }

    public async Task<Guid> ScheduleAsync(Guid announcementId, AnnouncementLifecycleWork work, DateTime dueUtc, CancellationToken token)
    {
        var tenantId = _tenantProvider.TenantId.ToString();
        var scheduled = await _scheduler.SchedulePublish(
            DateTime.SpecifyKind(dueUtc, DateTimeKind.Utc),
            new AnnouncementLifecycleDue(announcementId, work),
            Pipe.Execute<SendContext<AnnouncementLifecycleDue>>(ctx =>
                ctx.Headers.Set(TenantPublishFilter<AnnouncementLifecycleDue>.HeaderName, tenantId)),
            token);
        return scheduled.TokenId;
    }

    public Task CancelAsync(Guid jobToken, CancellationToken token) =>
        _scheduler.CancelScheduledPublish<AnnouncementLifecycleDue>(jobToken, token);
}
