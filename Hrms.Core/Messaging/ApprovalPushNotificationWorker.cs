using Hrms.Core.Services.Approvals;
using MassTransit;
using Onepunch.Common.Lib.DTO;

namespace Hrms.Core.Messaging;

// hrms-api self-consumes the SAME ApprovalNotificationRequested message it publishes (both
// processes share the same RabbitMQ broker -- see RabbitMqConfiguration/tenantstore's
// NotifTenantRabbitConfiguration) so the push channel can reuse NotificationHub without a new
// service. TenantConsumeFilter<T> resolves the correct tenant DB context for this consumer from
// the X-Tenant-ID header the publish side already stamps, same as every other consumer here --
// not that this worker needs tenant DB access itself, since everything it needs already rode
// along on the message.
public class ApprovalPushNotificationWorker : IConsumer<ApprovalNotificationRequested>
{
    private readonly ApprovalPushNotificationService _pushService;

    public ApprovalPushNotificationWorker(ApprovalPushNotificationService pushService)
    {
        _pushService = pushService;
    }

    public async Task Consume(ConsumeContext<ApprovalNotificationRequested> context)
    {
        var message = context.Message;
        if (!message.DeliverPush || message.RecipientUserId is not { } userId) return;

        await _pushService.PushToUserAsync(userId, new ApprovalPushNotification
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
        }, context.CancellationToken);
    }
}
