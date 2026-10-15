using Hrms.Core.Policies.OtherIncome.Eligibility;
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

            // Attendance-mode rows that didn't qualify are flagged BEFORE the stamp below, while
            // they're still unconsumed (the evaluator only looks at unconsumed rows). They get
            // stamped with the rest, so a skipped day never carries into a later run.
            var skipped = await OtherIncomeEligibilityEvaluator.FindSkippedAsync(
                Context, [payroll.EmployeeId], payroll.PayPeriodStart, payroll.PayPeriodEnd, token);
            foreach (var group in skipped.GroupBy(x => x.Value, x => x.Key))
            {
                var ids = group.ToList();
                var reason = group.Key;
                await Context.OtherIncomeApplicationDetails
                    .Where(x => ids.Contains(x.Id))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.IsSkipped, true)
                        .SetProperty(x => x.SkipReason, reason), token);
            }

            await Context.OtherIncomeApplicationDetails
                .Where(x => x.EmployeeId == payroll.EmployeeId &&
                            x.Date >= payroll.PayPeriodStart &&
                            x.Date <= payroll.PayPeriodEnd &&
                            x.ConsumedByPayrollId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedByPayrollId, payroll.Id), token);

            await FlagDeductionMissesAsync(payroll, token);
        }
    }

    // Deduction installments that failed their attendance rule in this run (the same verdicts
    // DeductionAplDtlService.LoadAsync left them out for). Unlike Other Income there is no
    // "consumed" stamp to set -- a deduction is paid down at Post -- so the verdict itself is the
    // record: Skip rows never come back, Defer rows are charged by the next run without being
    // judged again. The run's id lets deleting its batch release them (ReleaseAsync).
    private async Task FlagDeductionMissesAsync(Payroll payroll, CancellationToken token)
    {
        var misses = await DeductionEligibilityEvaluator.FindMissesAsync(
            Context, [payroll.EmployeeId], payroll.PayPeriodEnd, token);

        foreach (var group in misses.GroupBy(x => (x.Value.Action, x.Value.Reason), x => x.Key))
        {
            var ids = group.ToList();
            var (action, reason) = group.Key;
            var skip = action == AttendanceMissAction.Skip;
            await Context.DeductionApplicationDetails
                .Where(x => ids.Contains(x.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsSkipped, skip)
                    .SetProperty(x => x.IsDeferred, !skip)
                    .SetProperty(x => x.SkipReason, reason)
                    .SetProperty(x => x.SkippedByPayrollId, (Guid?)payroll.Id), token);
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

        // Every employee's, confidential ones included -- the whole run is being removed.
        await Context.IgnoringConfidentiality<SalaryAdjustment>()
            .Where(x => x.ConsumedByPayrollId != null && payrollIds.Contains(x.ConsumedByPayrollId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedByPayrollId, (Guid?)null), token);

        await Context.IgnoringConfidentiality<OtherIncomeSchedules>()
            .Where(x => x.ConsumedByPayrollId != null && payrollIds.Contains(x.ConsumedByPayrollId.Value))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ConsumedByPayrollId, (Guid?)null)
                // A skipped day is re-judged by the next run, so its verdict is cleared too.
                .SetProperty(x => x.IsSkipped, false)
                .SetProperty(x => x.SkipReason, (string?)null), token);

        // Same for a deduction installment this run skipped or deferred.
        await Context.DeductionApplicationDetails
            .Where(x => x.SkippedByPayrollId != null && payrollIds.Contains(x.SkippedByPayrollId.Value))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsSkipped, false)
                .SetProperty(x => x.IsDeferred, false)
                .SetProperty(x => x.SkipReason, (string?)null)
                .SetProperty(x => x.SkippedByPayrollId, (Guid?)null), token);
    }
}
