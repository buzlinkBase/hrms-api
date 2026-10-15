using Hrms.Domain.Entities;
using Hrms.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Core.Policies.OtherIncome.Eligibility;

// A deduction installment that failed its attendance rule, and what the application says to do
// about it.
public sealed record DeductionMiss(string Reason, AttendanceMissAction Action);

// The Deduction half of attendance-based schedules. Unlike Other Income rows, installments carry
// over by design (DeductionAplDtlService.LoadAsync has no lower date bound), so there is no window
// start here: every approved, still-open, not-yet-judged row due on or before toDate is a candidate.
// A row a saved run already skipped or deferred is never judged again -- a deferred row is charged
// by the next run unconditionally.
public static class DeductionEligibilityEvaluator
{
    // Row id -> miss for every candidate that does NOT qualify. FixedSchedule rows are absent.
    public static async Task<Dictionary<Guid, DeductionMiss>> FindMissesAsync(
        HrmsContext context, IReadOnlyCollection<Guid> employeeIds, DateOnly toDate, CancellationToken token)
    {
        if (employeeIds.Count == 0) return new Dictionary<Guid, DeductionMiss>();

        var rows = await (
            from row in context.DeductionApplicationDetails
            join app in context.DeductionApplications on row.DeductionApplicationId equals app.Id
            where row.Date <= toDate
                  && row.Balance > 0
                  && !row.IsSkipped && !row.IsDeferred
                  && employeeIds.Contains(row.EmployeeId)
                  && app.ApprovalStatus == ApprovalStatus.Approved
                  && app.PayMode != OtherIncomePayMode.FixedSchedule
            select new
            {
                row.Id,
                row.EmployeeId,
                row.Date,
                row.PeriodStart,
                row.PeriodEnd,
                app.PayMode,
                app.AttendanceBasis,
                app.MinHours,
                app.MaxLateMinutes,
                app.MaxUndertimeMinutes,
                app.ApplicableDays,
                app.DayTypes,
                app.MinQualifyingDays,
                app.OnMiss,
            })
            .AsNoTracking()
            .ToListAsync(token);

        var actions = rows.ToDictionary(x => x.Id, x => x.OnMiss);
        var candidates = rows
            .Select(x => new AttendanceCandidate(
                x.Id, x.EmployeeId, x.Date, x.PeriodStart, x.PeriodEnd,
                new OtherIncomeRule(x.PayMode, x.AttendanceBasis, x.MinHours, x.MaxLateMinutes,
                    x.MaxUndertimeMinutes, x.ApplicableDays, x.DayTypes, x.MinQualifyingDays)))
            .ToList();

        var failed = await AttendanceEligibilityJudge.JudgeAsync(context, candidates, token);
        return failed.ToDictionary(x => x.Key, x => new DeductionMiss(x.Value, actions[x.Key]));
    }
}
