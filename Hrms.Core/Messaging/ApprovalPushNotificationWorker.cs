using Hrms.Core.Services.Approvals;
using MassTransit;
using Onepunch.Common.Lib.DTO;

namespace Hrms.Core.Messaging;

// hrms-api self-consumes the SAME ApprovalNotificationRequested message it publishes (both
// processes share the same RabbitMQ broker -- see RabbitMqConfiguration/tenantstore's
// NotifTenantRabbitConfiguration) so the push channel can reuse NotificationHub without a new
// service. TenantConsumeFilter<T> resolves the tenant from the X-Tenant-ID header the publish
// side already stamps, same as every other consumer here.
public class ApprovalPushNotificationWorker : IConsumer<ApprovalNotificationRequested>
{
    private readonly ApprovalPushNotificationService _pushService;
    private readonly ITenantProvider _tenantProvider;
    private readonly Services.Notifications.UserNotificationService _notifications;

    public ApprovalPushNotificationWorker(
        ApprovalPushNotificationService pushService,
        ITenantProvider tenantProvider,
        Services.Notifications.UserNotificationService notifications)
    {
        _pushService = pushService;
        _tenantProvider = tenantProvider;
        _notifications = notifications;
    }

    public async Task Consume(ConsumeContext<ApprovalNotificationRequested> context)
    {
        var message = context.Message;
        if (!message.DeliverPush) return;

        var notification = new ApprovalPushNotification
        {
            ApplicationType = EnumParserConfig.SafeParseEnum(message.ApplicationType, ApprovalApplicationType.Leave),
            ApplicationId = message.ApplicationId,
            ApprovalInstanceId = message.ApprovalInstanceId,
            ApplicationTypeLabel = message.ApplicationTypeLabel,
            ApplicantName = message.ApplicantName,
            StatusLabel = message.StatusLabel,
            StepNumber = message.StepNumber,
            TotalSteps = message.TotalSteps,
            Note = message.Note,
        };

        // Saved first, so the bell still has it after a refresh or for someone offline right now;
        // the push then carries the saved id. Keyed by the message id, so a retry after a failed
        // push reuses the entry instead of adding a second.
        if (!string.IsNullOrWhiteSpace(message.RecipientEmail) || message.RecipientUserId != null)
        {
            var saved = await _notifications.SaveApprovalAsync(
                context.MessageId ?? NewId.NextGuid(), message.RecipientUserId, message.RecipientEmail, notification, context.CancellationToken);
            notification.Id = saved.Id;
            notification.TimestampUtc = saved.CreatedAt;
        }

        // By company + login email first: reaches accounts matched to their employee record by
        // email as well as by UserId (see ApprovalRecipientGroups). One target per message, so a
        // connection never receives the same push twice.
        if (!string.IsNullOrWhiteSpace(message.RecipientEmail) && _tenantProvider.TenantId != Guid.Empty)
        {
            await _pushService.PushToRecipientAsync(_tenantProvider.TenantId, message.RecipientEmail, notification, context.CancellationToken);
            return;
        }

        if (message.RecipientUserId is { } userId)
            await _pushService.PushToUserAsync(userId, notification, context.CancellationToken);
    }
}
