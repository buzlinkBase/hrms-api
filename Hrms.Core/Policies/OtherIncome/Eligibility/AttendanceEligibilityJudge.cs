using Hrms.Domain.Entities;
using Hrms.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Core.Policies.OtherIncome.Eligibility;

// One schedule row (an Other Income payment or a Deduction installment) to be judged against
// attendance. Period is set for a PeriodicGated row; otherwise the row's own Date is the day.
public sealed record AttendanceCandidate(
    Guid Id, Guid EmployeeId, DateOnly Date, DateOnly? PeriodStart, DateOnly? PeriodEnd, OtherIncomeRule Rule);

// The database half shared by every attendance-based schedule: loads the posted DTR the candidates
// need and runs the rule chain. Feature evaluators (OtherIncomeEligibilityEvaluator,
// DeductionEligibilityEvaluator) only decide WHICH rows are candidates.
public static class AttendanceEligibilityJudge
{
    // Row id -> OtherIncomeSkipReasons code for every candidate that does NOT qualify.
    public static async Task<Dictionary<Guid, string>> JudgeAsync(
        HrmsContext context, IReadOnlyCollection<AttendanceCandidate> candidates, CancellationToken token)
    {
        var failed = new Dictionary<Guid, string>();
        if (candidates.Count == 0) return failed;

        var records = await LoadRecordsAsync(
            context,
            candidates.Select(x => x.EmployeeId).Distinct().ToList(),
            candidates.Min(x => x.PeriodStart ?? x.Date),
            candidates.Max(x => x.PeriodEnd ?? x.Date),
            token);

        foreach (var candidate in candidates)
        {
            records.TryGetValue(candidate.EmployeeId, out var byDate);
            byDate ??= new Dictionary<DateOnly, DailyRecord>();

            var reason = candidate.Rule.PayMode == OtherIncomePayMode.PeriodicGated
                ? PeriodGate.Evaluate(
                    candidate.PeriodStart ?? candidate.Date, candidate.PeriodEnd ?? candidate.Date,
                    candidate.Rule, byDate)
                : DayEligibilityChain.Full.Evaluate(
                    new DayContext(candidate.Date, byDate.GetValueOrDefault(candidate.Date), candidate.Rule));

            if (reason != null) failed[candidate.Id] = reason;
        }
        return failed;
    }

    // Posted DTR per employee per day, regardless of PayrollBatchId: a weekly/monthly window can
    // reach back into days an earlier run already paid. When one date has several records the one
    // with worked hours wins, as in EmployeePayrollLineService.SelectDtrRecordForDate.
    private static async Task<Dictionary<Guid, Dictionary<DateOnly, DailyRecord>>> LoadRecordsAsync(
        HrmsContext context, List<Guid> employeeIds, DateOnly fromDate, DateOnly toDate, CancellationToken token)
    {
        var records = await context.DailyTimeRecords
            .AsNoTracking()
            .Where(x => x.Posted && employeeIds.Contains(x.EmployeeId)
                        && x.WorkDate >= fromDate && x.WorkDate <= toDate)
            .ToListAsync(token);

        return records
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.WorkDate)
                    .ToDictionary(d => d.Key, d => d.OrderByDescending(x => x.TotalHours).First()));
    }
}
