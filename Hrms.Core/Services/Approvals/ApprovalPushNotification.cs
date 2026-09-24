namespace Hrms.Core.Services.Approvals;

// Pushed to the frontend over NotificationHub whenever ApprovalPushNotificationWorker consumes
// an ApprovalNotificationRequested message with DeliverPush == true. Carries ApplicationType/
// ApplicationId so the frontend can invalidate exactly the ["approval-instance", applicationType,
// applicationId] / ["approval-eligibility", ...] query keys ApprovalStatusCell/ApprovalTimeline
// already read (src/shared/hooks/use-approval-queries.ts), rather than a broad refetch-everything.
public sealed class ApprovalPushNotification
{
    public ApprovalApplicationType ApplicationType { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid ApprovalInstanceId { get; set; }
    public string ApplicationTypeLabel { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    // "Pending Your Approval" | "Approved" | "Declined" -- mirrors ApprovalNotificationRequested.
    public string StatusLabel { get; set; } = string.Empty;
    public int? StepNumber { get; set; }
    public int? TotalSteps { get; set; }
    public string? Note { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
