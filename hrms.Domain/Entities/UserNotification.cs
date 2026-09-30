using BuzlinkRepository;

namespace Hrms.Domain.Entities;

/// <summary>
/// One entry in a person's notification bell -- today an approval update (waiting on you, moved
/// to the next step, approved, declined). Saved so the bell survives a refresh and reaches people
/// who were offline when it happened; SignalR only delivers it live. The recipient is whoever the
/// push targets: their login email in this company, else their account id. Kept for a limited
/// time (NotificationRetentionJob). Pool pushes to every eligible approver aren't saved -- those
/// requests are in the approval queue.
/// </summary>
[DisableSoftDelete]
public class UserNotification : BaseEntity
{
    public const string ApprovalType = "Approval";

    public Guid? RecipientUserId { get; set; }
    /// <summary>Trimmed, lower-case.</summary>
    public string? RecipientEmail { get; set; }
    public string Type { get; set; } = ApprovalType;

    public ApprovalApplicationType ApplicationType { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid ApprovalInstanceId { get; set; }
    public string ApplicationTypeLabel { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public int? StepNumber { get; set; }
    public int? TotalSteps { get; set; }
    public string? Note { get; set; }

    public DateTime? ReadAt { get; set; }
}
