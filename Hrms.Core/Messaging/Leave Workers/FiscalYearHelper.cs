namespace Hrms.Core.Messaging.LeaveWorkers;

/// <summary>
/// Fiscal-year date arithmetic. All logic is driven by <c>startMonth</c>
/// (the first month of the fiscal year, 1–12).
/// When <c>startMonth == 1</c> the fiscal year is identical to the calendar year.
/// Example: startMonth=4 → FY2025 = Apr 2025 – Mar 2026.
/// </summary>
public static class FiscalYearHelper
{
    /// <summary>
    /// Returns the fiscal-year label (integer) that the given date falls in.
    /// The label is the calendar year in which the fiscal year STARTS.
    /// </summary>
    public static int FiscalYearOf(DateOnly date, int startMonth) =>
        date.Month >= startMonth ? date.Year : date.Year - 1;

    /// <summary>Returns the UTC start of the fiscal year period.</summary>
    public static DateTime PeriodStart(int fiscalYear, int startMonth) =>
        new(fiscalYear, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Returns the UTC end (23:59:59) of the fiscal year period.</summary>
    public static DateTime PeriodEnd(int fiscalYear, int startMonth)
    {
        if (startMonth == 1)
            return new DateTime(fiscalYear, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var endYear  = fiscalYear + 1;
        var endMonth = startMonth - 1;
        return new DateTime(endYear, endMonth, DateTime.DaysInMonth(endYear, endMonth), 23, 59, 59, DateTimeKind.Utc);
    }

    /// <summary>Returns the last date (DateOnly) of the fiscal year — the day carry-over fires.</summary>
    public static DateOnly LastDayOfFiscalYear(int fiscalYear, int startMonth)
    {
        var end = PeriodEnd(fiscalYear, startMonth);
        return new DateOnly(end.Year, end.Month, end.Day);
    }

    /// <summary>True when <paramref name="date"/> is the first day of a fiscal year.</summary>
    public static bool IsFiscalYearStart(DateOnly date, int startMonth) =>
        date.Month == startMonth && date.Day == 1;

    /// <summary>True when <paramref name="date"/> is the last day of a fiscal year.</summary>
    public static bool IsFiscalYearEnd(DateOnly date, int startMonth)
    {
        var closeMonth = startMonth == 1 ? 12 : startMonth - 1;
        return date.Month == closeMonth
            && date.Day == DateTime.DaysInMonth(date.Year, closeMonth);
    }
}
