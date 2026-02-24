using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

public class ThirteenthMonthLedger : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal Amount { get; set; }
    public string? Remarks { get; set; }
}