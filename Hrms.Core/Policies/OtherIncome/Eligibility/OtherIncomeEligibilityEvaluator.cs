using Hrms.Domain.Entities;
using Hrms.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Core.Policies.OtherIncome.Eligibility;

// The Other Income half of attendance-based schedules. Finds the unconsumed schedule rows of
// attendance-mode applications and judges each against the posted DTR. Both the payroll read side
// (IncomeAplDtlService.LoadAsync) and the save side (PayrollInputConsumptionService) call this one
// method, so what a run previews as skipped is exactly what the save marks as skipped.
public static class OtherIncomeEligibilityEvaluator
{
    // Row id -> OtherIncomeSkipReasons code for every row that does NOT qualify. Rows that qualify,
    // and FixedSchedule rows (no attendance rule), are absent from the result.
    public static async Task<Dictionary<Guid, string>> FindSkippedAsync(
        HrmsContext context, IReadOnlyCollection<Guid> employeeIds, DateOnly fromDate, DateOnly toDate,
        CancellationToken token)
    {
        if (employeeIds.Count == 0) return new Dictionary<Guid, string>();

        var rows = await (
            from row in context.OtherIncomeApplicationDetails
            join app in context.OtherIncomeApplications on row.OtherIncomeApplicationId equals app.Id
            where row.Status == "Active"
                  && row.ConsumedByPayrollId == null
                  && row.Date >= fromDate && row.Date <= toDate
                  && employeeIds.Contains(row.EmployeeId)
                  && (app.PayMode == OtherIncomePayMode.PerQualifyingDay
                      || app.PayMode == OtherIncomePayMode.PeriodicGated)
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
            })
            .AsNoTracking()
            .ToListAsync(token);

        var candidates = rows
            .Select(x => new AttendanceCandidate(
                x.Id, x.EmployeeId, x.Date, x.PeriodStart, x.PeriodEnd,
                new OtherIncomeRule(x.PayMode, x.AttendanceBasis, x.MinHours, x.MaxLateMinutes,
                    x.MaxUndertimeMinutes, x.ApplicableDays, x.DayTypes, x.MinQualifyingDays)))
            .ToList();
        return await AttendanceEligibilityJudge.JudgeAsync(context, candidates, token);
    }
}
