namespace Hrms.Domain.Entities;

public class SalaryAdjustment : BaseEntity
{
    public SalaryAdjustmentType AdjustmentType { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal Amount { get; set; }
    public string? Remarks { get; set; }
    // Null = available to be picked up by a payroll run. Stamped with the Payroll.Id that
    // actually applied it once that run is SAVED (not merely calculated/previewed) — see
    // EmployeePayrollLineService.ApplySalaryAdjustments and
    // PayrollInputConsumptionService.MarkConsumedAsync. Nulled back out if that batch is
    // later deleted (PayrollBatchLifecycleService.DeleteBatchAsync), releasing the row for
    // the next run.
    public Guid? ConsumedByPayrollId { get; set; }
}
