namespace Hrms.Domain.ValueObjects;

public class CreateEmployeeProfileUpdateRequest
{
    public Guid EmployeeId { get; set; }
    public string? NewContact { get; set; }
    public string? NewAddress1 { get; set; }
    public string? NewAddress2 { get; set; }
    public string? NewCivilStatus { get; set; }
    public DateTime? NewDOB { get; set; }
    public string? NewBloodType { get; set; }
    public string? Remarks { get; set; }
}

public class EmployeeProfileUpdateRequestModel : CreateEmployeeProfileUpdateRequest
{
    public Guid Id { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime CreatedAt { get; set; }

    // The employee's CURRENT values as of right now (not at submission time) -- lets an approver
    // see exactly what's changing, current vs. requested, side by side.
    public string? CurrentContact { get; set; }
    public string? CurrentAddress1 { get; set; }
    public string? CurrentAddress2 { get; set; }
    public string? CurrentCivilStatus { get; set; }
    public DateTime? CurrentDOB { get; set; }
    public string? CurrentBloodType { get; set; }

    // True when Employee has been modified since this request's own snapshot was taken -- see
    // EmployeeProfileUpdateRequest.EmployeeSnapshotUpdatedAt.
    public bool HasConflict { get; set; }
}
