using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// Shared by LeaveApplicationService.EnsurePolicyAsync (filing-time check) and
// LeavePeriodGrantWorker (accrual-time check) -- previously each had its own slightly
// divergent MonthsBetween copy; this also adds the Leave.EligibilityBasis.PresentDays branch
// so both call sites treat it identically.
public static class LeaveEligibilityCalculator
{
    // WorkType values that represent a genuine gap in the employee's service record for
    // present-days eligibility counting -- every other WorkType, including holidays and rest
    // days (whether worked or not), counts as a "present" day.
    public static readonly List<WorkType> NonPresentWorkTypes = [WorkType.Absent, WorkType.Incomplete, WorkType.Skipped];

    // Whole months elapsed between two dates -- e.g. hired 2026-01-15, filing on 2026-06-10 is
    // only 4 completed months (the day-of-month hasn't come around again yet), not 5.
    public static int MonthsBetween(DateOnly from, DateOnly to)
    {
        var months = (to.Year - from.Year) * 12 + (to.Month - from.Month);
        if (to.Day < from.Day) months--;
        return Math.Max(months, 0);
    }

    public static bool IsServiceRequirementMet(Leave leave, int monthsServed, int presentDays) =>
        leave.EligibilityBasis == LeaveEligibilityBasis.PresentDays
            ? presentDays >= leave.MinPresentDays
            : monthsServed >= leave.MinServiceMonths;

    public static string ServiceRequirementMessage(Leave leave, int monthsServed, int presentDays) =>
        leave.EligibilityBasis == LeaveEligibilityBasis.PresentDays
            ? $"This employee has {presentDays} present day{(presentDays != 1 ? "s" : "")}. " +
              $"\"{leave.Description}\" requires at least {leave.MinPresentDays} present day{(leave.MinPresentDays != 1 ? "s" : "")}."
            : $"This employee has {monthsServed} month{(monthsServed != 1 ? "s" : "")} of service. " +
              $"\"{leave.Description}\" requires at least {leave.MinServiceMonths} month{(leave.MinServiceMonths != 1 ? "s" : "")}.";
}
