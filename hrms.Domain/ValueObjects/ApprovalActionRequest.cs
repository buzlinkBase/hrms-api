namespace Hrms.Domain.ValueObjects;

// Body for the dedicated approve/decline endpoints (Pass Slip, Loan/Deduction) that don't
// already carry an ApprovalStatus field on their own update payload. Leave/Overtime/Official
// Business instead carry Note directly on their Update*Application payload since those route
// approve/decline through the same conflated PUT as a plain edit.
public class ApprovalActionRequest
{
    public string? Note { get; set; }
}

// Approve body for Profile Update requests -- ForceApply lets the approver proceed despite a
// conflict (see EmployeeProfileUpdateRequest.EmployeeSnapshotUpdatedAt) after reviewing it.
public class ApproveProfileUpdateRequest : ApprovalActionRequest
{
    public bool ForceApply { get; set; }
}
