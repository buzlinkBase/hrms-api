namespace Hrms.Domain.ValueObjects;

// Optional "which employees" scope for a report. Every member is optional; a report applies only
// the ones that are set, so an empty filter means everyone (the report's original behaviour).
public class EmployeeScopeFilter
{
    public Guid? ClientId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? BranchId { get; set; }

    public bool IsEmpty =>
        ClientId == null && AreaId == null && PayrollGroupId == null
        && DepartmentId == null && BranchId == null;
}
