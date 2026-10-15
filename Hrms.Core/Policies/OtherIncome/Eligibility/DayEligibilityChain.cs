using Hrms.Domain.Entities;

namespace Hrms.Core.Policies.OtherIncome.Eligibility;

// Chain of Responsibility: the rules run in order and the first failure ends the check.
// Two chains are exposed because a periodic gate needs them separately -- Applicability decides
// which days the gate expects at all, Attendance decides which of those days qualified.
public sealed class DayEligibilityChain
{
    public static readonly DayEligibilityChain Applicability = new(
        new ApplicableWeekdayRule(), new DayTypeRule());

    public static readonly DayEligibilityChain Attendance = new(
        new AttendanceBasisRule(), new MinHoursRule(), new PunctualityRule());

    // Applicability first, then attendance -- what a PerQualifyingDay row is judged by.
    public static readonly DayEligibilityChain Full = new(
        new ApplicableWeekdayRule(), new DayTypeRule(),
        new AttendanceBasisRule(), new MinHoursRule(), new PunctualityRule());

    private readonly IReadOnlyList<IDayEligibilityRule> _rules;

    private DayEligibilityChain(params IDayEligibilityRule[] rules) => _rules = rules;

    // Null = the day passes; otherwise the OtherIncomeSkipReasons code of the first failing rule.
    public string? Evaluate(DayContext day)
    {
        foreach (var rule in _rules)
        {
            var failure = rule.Check(day);
            if (failure != null) return failure;
        }
        return null;
    }
}

// PeriodicGated: one payment per window, paid in full only when enough of the window's expected
// days qualified.
public static class PeriodGate
{
    // Returns null when the gate is met, else GateNotMet. Expected days are the window's days that
    // pass the Applicability chain; a window with none expected is not met -- nothing was earned.
    public static string? Evaluate(
        DateOnly start, DateOnly end, OtherIncomeRule rule, IReadOnlyDictionary<DateOnly, DailyRecord> records)
    {
        var expected = 0;
        var qualified = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            records.TryGetValue(date, out var record);
            var day = new DayContext(date, record, rule);
            if (DayEligibilityChain.Applicability.Evaluate(day) != null) continue;
            expected++;
            if (DayEligibilityChain.Attendance.Evaluate(day) == null) qualified++;
        }

        if (expected == 0) return OtherIncomeSkipReasons.GateNotMet;
        var required = Math.Min(rule.MinQualifyingDays ?? expected, expected);
        return qualified >= required ? null : OtherIncomeSkipReasons.GateNotMet;
    }
}
