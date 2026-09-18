namespace Hrms.Domain.Entities.EmployeeEntities;

// Employee self-service edit of their own personal info, staged for approval -- the "temp
// employee detail" store. Nothing here touches the real Employee row until
// EmployeeProfileUpdateRequestService.ApproveAsync's final-approval hook copies these New*
// fields onto it. See EmployeeSnapshotUpdatedAt for the conflict-detection story: if Employee is
// edited directly (e.g. by HR) while this request is still pending, that snapshot no longer
// matches Employee.UpdatedAt by the time an approver acts, and ApproveAsync surfaces that as a
// conflict instead of silently overwriting the more recent edit.
public class EmployeeProfileUpdateRequest : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }

    // Proposed values -- always submitted as a full set (the form is pre-filled with the
    // employee's current values, they edit what they want changed), not a partial diff.
    public string? NewContact { get; set; }
    public string? NewAddress1 { get; set; }
    public string? NewAddress2 { get; set; }
    public string? NewCivilStatus { get; set; }
    public DateTime? NewDOB { get; set; }
    public string? NewBloodType { get; set; }

    // The employee's own note on why they're requesting this change -- distinct from
    // ApprovalAction.Note, which is the approver's note on their decision.
    public string? Remarks { get; set; }

    // Employee.UpdatedAt at the moment this request was submitted -- see class doc comment.
    public DateTime? EmployeeSnapshotUpdatedAt { get; set; }

    public virtual ICollection<EmployeeProfileUpdateRequestDocument> Documents { get; set; } = new List<EmployeeProfileUpdateRequestDocument>();
}

// One or many supporting documents an employee can attach to a profile-update request.
// Provisioned now so a future "attach proof" feature is additive; unused/unwired in this change
// -- no upload endpoint or UI writes to this table yet.
public class EmployeeProfileUpdateRequestDocument : BaseEntity
{
    public Guid EmployeeProfileUpdateRequestId { get; set; }
    public virtual EmployeeProfileUpdateRequest? Request { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}
