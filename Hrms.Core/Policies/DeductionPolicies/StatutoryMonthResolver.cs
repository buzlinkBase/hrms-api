namespace Hrms.Core.Policies.DeductionPolicies;

/// <summary>
/// Which calendar month a payroll period's SSS / PhilHealth / Pag-IBIG belong to -- the one
/// answer the cutoff position (CutoffPolicyResolver), the credited ledger row
/// (PayrollSummaryLine.StatutoryCreditDate) and the month's prior gross and withholding
/// (StatutoryHelper, the *Helper.GetBalance methods) must all agree on.
/// <para>
/// A payroll group whose last cutoff isn't the month's end (cutoffs 10/25, 1/15) runs periods
/// that roll over the month by design: Feb 26–Mar 10 and Mar 11–25 are March's two runs. Its
/// month is always the month the period ENDS in, whatever the company's cross-month policy says
/// -- that policy exists for the occasional period that crosses a month boundary in a group
/// whose cutoffs end at month-end (15/EOM), where it still applies.
/// </para>
/// </summary>
public static class StatutoryMonthResolver
{
    /// <summary>The group's cutoffs don't reach the month's end, so its periods roll over months.</summary>
    public static bool RollsOverMonth(PayrollGroupModel? group)
    {
        var days = group?.CutoffDays;
        if (days is not { Count: > 0 }) return false;
        // A fixed day of 28 or later is the month's end in practice (February has no 30th).
        return !days.Any(d => d.IsEndOfMonth) && days.Max(d => d.Day) < 28;
    }

    /// <summary>The date a period's contributions are credited to.</summary>
    public static DateOnly CreditDate(
        PayrollGroupModel? group, DateOnly from, DateOnly to, CrossMonthStatutoryCreditPolicy policy, DateOnly? payDate) =>
        RollsOverMonth(group) ? to : StatutoryCreditDateResolver.Resolve(from, to, policy, payDate);

    public static bool SameMonth(DateOnly a, DateOnly b) => a.Year == b.Year && a.Month == b.Month;

    /// <summary>The first day of the month of <paramref name="date"/>.</summary>
    public static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);

    /// <summary>The last day of the month of <paramref name="date"/>.</summary>
    public static DateOnly MonthEnd(DateOnly date) => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
}
