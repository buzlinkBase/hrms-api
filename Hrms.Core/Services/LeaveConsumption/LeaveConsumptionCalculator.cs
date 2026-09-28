using Hrms.Domain.Entities;

namespace Hrms.Core.Services.LeaveConsumption;

/// <summary>Result of <see cref="LeaveConsumptionCalculator.Compute"/>.</summary>
/// <param name="DaysByApplication">Leave days each application consumed, in DAYS (the unit
/// LeaveCredits and every ledger entry use).</param>
/// <param name="ExcludedDays">Leave days found on the DTR rows that no approved application
/// covers (deleted, declined, cancelled or still pending) -- never charged.</param>
public record LeaveConsumptionResult(IReadOnlyDictionary<Guid, decimal> DaysByApplication, int ExcludedDays);

/// <summary>
/// Turns posted DTR rows into leave days consumed per leave application. Pure (no DB), shared by
/// DTR posting (LeaveDtrReconciliationService.ConsumeReservationsAsync) and the repair tool
/// (RepairPostedDeductionsAsync) so both charge exactly the same amount.
///
/// Rules:
/// - DtrLeaveMetaData.Hours is real HOURS; the ledger is in DAYS. A day's hours are converted
///   against that day's shift length (ShiftWorkingHour, or 8 when unknown) and capped at 1 day.
///   Previously the raw hours were deducted as days, so an 8-hour leave day cost 8 days.
/// - Each employee/date/leave type is charged once, however many LeavesInfo rows it carries.
/// - Only applications passed in (the caller supplies the currently approved, non-deleted ones)
///   can consume. A leave day no such application covers is excluded, never charged.
/// - When two applications of the same leave type cover the same day, the earliest filed wins,
///   so the day is never charged twice.
/// </summary>
public static class LeaveConsumptionCalculator
{
    public const double DefaultShiftHours = 8;

    public static LeaveConsumptionResult Compute(
        IEnumerable<DailyRecord> records,
        IReadOnlyCollection<LeaveApplication> approvedApplications)
    {
        var days = new Dictionary<Guid, decimal>();
        var excluded = 0;

        // One charge per employee/date/leave type -- duplicate LeavesInfo rows collapse here.
        var leaveDays = records
            .Where(r => r.LeavesInfo is { Count: > 0 })
            .SelectMany(r => r.LeavesInfo!.Select(li => new { r.EmployeeId, r.WorkDate, r.ShiftWorkingHour, li.LeaveId, li.Hours }))
            .Where(x => x.Hours > 0)
            .GroupBy(x => (x.EmployeeId, x.WorkDate, x.LeaveId))
            .Select(g => new { g.Key.EmployeeId, g.Key.WorkDate, g.Key.LeaveId, g.First().ShiftWorkingHour, Hours = g.Max(x => x.Hours) });

        foreach (var day in leaveDays)
        {
            var application = approvedApplications
                .Where(a => a.EmployeeId == day.EmployeeId
                    && a.LeaveId == day.LeaveId
                    && a.LeaveDateFrom <= day.WorkDate
                    && a.LeaveDateTo >= day.WorkDate)
                .OrderBy(a => a.CreatedAt)
                .ThenBy(a => a.Id)
                .FirstOrDefault();

            if (application is null)
            {
                excluded++;
                continue;
            }

            days[application.Id] = days.GetValueOrDefault(application.Id) + ToDays(day.Hours, day.ShiftWorkingHour);
        }

        return new LeaveConsumptionResult(days, excluded);
    }

    /// <summary>Hours of leave on one day, as a fraction of that day's shift (max 1), 2 dp.</summary>
    public static decimal ToDays(double hours, double shiftWorkingHours)
    {
        var shift = shiftWorkingHours > 0 ? shiftWorkingHours : DefaultShiftHours;
        return Math.Round((decimal)Math.Min(1d, hours / shift), 2, MidpointRounding.AwayFromZero);
    }
}
