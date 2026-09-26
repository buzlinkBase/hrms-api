using Hrms.Core.Services.Approvals;
using MassTransit;

namespace Hrms.Core.Messaging;

// Fallback-step counterpart of ApprovalPushNotificationWorker: pushes to every connected
// approver of this type in the message's tenant (resolved from its X-Tenant-ID header by
// TenantConsumeFilter) rather than to one user.
public class ApprovalPoolPushNotificationWorker : IConsumer<ApprovalPoolNotificationRequested>
{
    private readonly ApprovalPushNotificationService _pushService;
    private readonly ITenantProvider _tenantProvider;

    public ApprovalPoolPushNotificationWorker(ApprovalPushNotificationService pushService, ITenantProvider tenantProvider)
    {
        _pushService = pushService;
        _tenantProvider = tenantProvider;
    }

    public async Task Consume(ConsumeContext<ApprovalPoolNotificationRequested> context)
    {
        var message = context.Message;
        if (_tenantProvider.TenantId == Guid.Empty) return;

        await _pushService.PushToApproversAsync(_tenantProvider.TenantId, new ApprovalPushNotification
        {
            ApplicationType = message.ApplicationType,
            ApplicationId = message.ApplicationId,
            ApprovalInstanceId = message.ApprovalInstanceId,
            ApplicationTypeLabel = message.ApplicationTypeLabel,
            ApplicantName = message.ApplicantName,
            StatusLabel = ApprovalEngineService.PendingApprovalStatusLabel,
            StepNumber = 1,
            TotalSteps = 1,
        }, context.CancellationToken);
    }
}
