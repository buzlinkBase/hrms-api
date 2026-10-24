using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Policies.DeductionPolicies;

// The window an employee's deductions are taken once in -- one cutoff of their OWN payroll group,
// whichever client run happens to be paying them. Runs for clients on different cutoffs all map a
// date to the same home period, which is what lets PayrollDeductionClaim hold exactly one carrier
// per employee per period.
public readonly record struct DeductionPeriod(DateOnly Start, DateOnly End);

public static class DeductionPeriodResolver
{
    public static DeductionPeriod Resolve(PayrollGroupModel? group, DateOnly date) =>
        Resolve(group?.PayrollFrequency, (group?.CutoffDays ?? []).Select(cd => (cd.Day, cd.IsEndOfMonth)), date);

    public static DeductionPeriod Resolve(PayrollGroup? group, DateOnly date) =>
        Resolve(group?.PayrollFrequency, (group?.CutoffDays ?? []).Select(cd => (cd.Day, cd.IsEndOfMonth)), date);

    // Monthly/Daily groups (and employees with no group or no cutoff days) deduct once a month.
    // Otherwise the period is the configured cutoff the date falls in: it starts the day after the
    // previous cutoff (or the 1st) and ends on the cutoff day; the last cutoff of a month always
    // runs to month end so no tail days are left without a period.
    public static DeductionPeriod Resolve(
        PayrollFrequency? frequency, IEnumerable<(int Day, bool IsEndOfMonth)> cutoffDays, DateOnly date)
    {
        var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
        var wholeMonth = new DeductionPeriod(new DateOnly(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, daysInMonth));
        if (frequency is null or PayrollFrequency.MONTHLY or PayrollFrequency.DAILY)
            return wholeMonth;

        var cutoffs = cutoffDays
            .Select(cd => cd.IsEndOfMonth ? daysInMonth : Math.Min(Math.Max(cd.Day, 1), daysInMonth))
            .Distinct()
            .Order()
            .ToList();
        if (cutoffs.Count == 0) return wholeMonth;

        // Days after the last configured cutoff belong to the last period, which therefore runs to month end.
        var index = cutoffs.FindIndex(d => date.Day <= d);
        if (index < 0) index = cutoffs.Count - 1;
        var startDay = index == 0 ? 1 : cutoffs[index - 1] + 1;
        var endDay = index == cutoffs.Count - 1 ? daysInMonth : cutoffs[index];
        return new DeductionPeriod(new DateOnly(date.Year, date.Month, startDay), new DateOnly(date.Year, date.Month, endDay));
    }
}
