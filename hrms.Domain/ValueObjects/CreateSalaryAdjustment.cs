namespace Hrms.Domain.ValueObjects;

public class CreateSalaryAdjustment
{
    public SalaryAdjustmentType AdjustmentType { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal Amount { get; set; }
    public string? Remarks { get; set; }

}
public class UpdateSalaryAdjustment : CreateSalaryAdjustment
{
    public Guid Id { get; set; }
}
public class SalaryAdjustmentModel : UpdateSalaryAdjustment { }


