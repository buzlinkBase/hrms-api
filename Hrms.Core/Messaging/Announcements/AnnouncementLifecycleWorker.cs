using Hrms.Core.Services.Announcements;
using Hrms.Core.Services.Announcements.Lifecycle;
using MassTransit;

namespace Hrms.Core.Messaging.Announcements;

// Runs one Quartz-scheduled AnnouncementLifecycleDue inside its company (TenantConsumeFilter set
// the tenant from the X-Tenant-ID header). Each service call re-checks the state and is a no-op
// when the work no longer applies -- e.g. the publisher already published it by hand, or
// restored it -- so a job that outlived its cancellation does nothing.
public class AnnouncementLifecycleWorker : IConsumer<AnnouncementLifecycleDue>
{
    private readonly AnnouncementService _announcements;

    public AnnouncementLifecycleWorker(AnnouncementService announcements)
    {
        _announcements = announcements;
    }

    public async Task Consume(ConsumeContext<AnnouncementLifecycleDue> context)
    {
        var message = context.Message;
        var token = context.CancellationToken;

        try
        {
            switch (message.Work)
            {
                case AnnouncementLifecycleWork.Publish:
                    await _announcements.PublishScheduledAsync(message.AnnouncementId, token);
                    break;
                case AnnouncementLifecycleWork.Expire:
                    await _announcements.ExpireAsync(message.AnnouncementId, token);
                    break;
                case AnnouncementLifecycleWork.Purge:
                    await _announcements.PurgeAsync(message.AnnouncementId, token);
                    break;
            }
        }
        catch (NotFoundException)
        {
            // Removed in the meantime -- nothing left to do.
        }
    }
}
