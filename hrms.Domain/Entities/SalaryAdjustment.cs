namespace Hrms.Domain.Entities;

public class SalaryAdjustment : BaseEntity
{
    public SalaryAdjustmentType AdjustmentType { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal Amount { get; set; }
    public string? Remarks { get; set; }
}
