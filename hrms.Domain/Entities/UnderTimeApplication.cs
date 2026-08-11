using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

public class UnderTimeApplication : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; }
    public DateOnly PayrollDate { get; set; }
    public double UTMinutes { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public ApprovalStatus ApprovalStatus  { get; set; }
}

