namespace Hrms.Core.Messaging;

// hrms-api-internal (published and consumed only here, never by tenantstore's NotificationApi):
// an application landed on the implicit fallback step, so there's no single recipient to address
// -- ApprovalPoolPushNotificationWorker pushes it to the whole approver group instead (see
// ApproverGroups). Published from ApprovalEngineService inside the same outbox transaction as
// ApprovalNotificationRequested, so it's only delivered once the application row is committed.
public record ApprovalPoolNotificationRequested
{
    public ApprovalApplicationType ApplicationType { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid ApprovalInstanceId { get; init; }
    public string ApplicationTypeLabel { get; init; } = string.Empty;
    public string ApplicantName { get; init; } = string.Empty;
}
