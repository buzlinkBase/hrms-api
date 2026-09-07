using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// Marks SalaryAdjustment/OtherIncomeSchedules rows as consumed once a payroll run that
// actually used them is SAVED — never at Calculate/preview time. Closes the gap where those
// entities had no "already applied" marker at all and were matched purely by date range,
// which risked the same row being applied twice by two different runs. See
// SalaryAdjustmentService.LoadAsync/FindAvailableAsync and
// IncomeAplDtlService.LoadAsync/FindAvailableAsync for the read side of this same fix, and
// PayrollBatchLifecycleService.DeleteBatchAsync for the release-on-delete side.
public class PayrollInputConsumptionService : BaseService<SalaryAdjustment>
{
    public PayrollInputConsumptionService(IUnitOfWorkService uow) : base(uow)
    {
    }

    // Regular payroll: implicitly consumes everything unconsumed in each saved line's own
    // date range for that employee — mirrors exactly what
    // EmployeePayrollLineService.ApplySalaryAdjustments/ComputeAllowances already matched
    // moments earlier during calculation, now persisted so a later overlapping run can't
    // re-match the same row.
    public async Task MarkConsumedByDateRangeAsync(List<Payroll> savedPayrolls, CancellationToken token)
    {
        foreach (var payroll in savedPayrolls)
        {
            await Context.SalaryAdjustments
                .Where(x => x.EmployeeId == payroll.EmployeeId &&
                            x.PayrollDate >= payroll.PayPeriodStart &&
                            x.PayrollDate <= payroll.PayPeriodEnd &&
                            x.ConsumedByPayrollId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedByPayrollId, payroll.Id), token);

            await Context.OtherIncomeApplicationDetails
                .Where(x => x.EmployeeId == payroll.EmployeeId &&
                            x.Date >= payroll.PayPeriodStart &&
                            x.Date <= payroll.PayPeriodEnd &&
                            x.ConsumedByPayrollId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedByPayrollId, payroll.Id), token);
        }
    }

    // Last Pay: only the explicitly HR-confirmed ids get stamped — never an implicit
    // date-range sweep, since Last Pay's own window (year start through separation) is far
    // wider than what should ever be applied automatically.
    public async Task MarkConsumedByIdsAsync(
        Guid payrollId, List<Guid>? salaryAdjustmentIds, List<Guid>? otherIncomeScheduleIds, CancellationToken token)
    {
        if (salaryAdjustmentIds is { Count: > 0 })
        {
            await Context.SalaryAdjustments
                .Where(x => salaryAdjustmentIds.Contains(x.Id) && x.ConsumedByPayrollId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedByPayrollId, payrollId), token);
        }

        if (otherIncomeScheduleIds is { Count: > 0 })
        {
            await Context.OtherIncomeApplicationDetails
                .Where(x => otherIncomeScheduleIds.Contains(x.Id) && x.ConsumedByPayrollId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedByPayrollId, payrollId), token);
        }
    }

    // Deleting a draft batch releases whatever it had claimed — mirrors the existing
    // DTR-unpost/statutory-ledger cleanup already done for a deleted batch in
    // PayrollBatchLifecycleService.DeleteBatchAsync.
    public async Task ReleaseAsync(List<Guid> payrollIds, CancellationToken token)
    {
        if (payrollIds.Count == 0) return;

        await Context.SalaryAdjustments
            .Where(x => x.ConsumedByPayrollId != null && payrollIds.Contains(x.ConsumedByPayrollId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedByPayrollId, (Guid?)null), token);

        await Context.OtherIncomeApplicationDetails
            .Where(x => x.ConsumedByPayrollId != null && payrollIds.Contains(x.ConsumedByPayrollId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedByPayrollId, (Guid?)null), token);
    }
}
