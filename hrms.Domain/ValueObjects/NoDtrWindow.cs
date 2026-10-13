using System.Linq.Expressions;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.ValueObjects;

/// <summary>
/// Single source of truth for the payroll-only ("no DTR") window on an employee: the flag plus an
/// optional Effective From / Until (a null bound is open-ended). Payroll selection, proration, the
/// DTR run and validation all ask this class instead of re-deriving the rule.
/// </summary>
public static class NoDtrWindow
{
    public static bool IsActiveOn(bool flag, DateOnly? from, DateOnly? until, DateOnly date) =>
        flag && (from == null || from <= date) && (until == null || until >= date);

    public static bool IsActiveOn(EmployeeSetting? s, DateOnly date) =>
        s != null && IsActiveOn(s.IsNoDTRNotRequired, s.NoDtrEffectiveFrom, s.NoDtrEffectiveUntil, date);

    public static bool IsActiveOn(CreateEmployeeSetting? s, DateOnly date) =>
        s != null && IsActiveOn(s.IsNoDTRNotRequired, s.NoDtrEffectiveFrom, s.NoDtrEffectiveUntil, date);

    /// <summary>Calendar days of [rangeFrom, rangeTo] inside the window; 0 when the flag is off.</summary>
    public static int OverlapDays(bool flag, DateOnly? from, DateOnly? until, DateOnly rangeFrom, DateOnly rangeTo)
    {
        if (!flag || rangeFrom > rangeTo) return 0;
        var start = from is { } f && f > rangeFrom ? f : rangeFrom;
        var end = until is { } u && u < rangeTo ? u : rangeTo;
        return start > end ? 0 : end.DayNumber - start.DayNumber + 1;
    }

    public static int OverlapDays(CreateEmployeeSetting? s, DateOnly rangeFrom, DateOnly rangeTo) =>
        s == null ? 0 : OverlapDays(s.IsNoDTRNotRequired, s.NoDtrEffectiveFrom, s.NoDtrEffectiveUntil, rangeFrom, rangeTo);

    /// <summary>Share of the range the window covers (1 = whole range, 0 = none) -- the proration factor.</summary>
    public static decimal CoverageFactor(CreateEmployeeSetting? s, DateOnly rangeFrom, DateOnly rangeTo)
    {
        var total = rangeTo.DayNumber - rangeFrom.DayNumber + 1;
        return total <= 0 ? 0m : (decimal)OverlapDays(s, rangeFrom, rangeTo) / total;
    }

    /// <summary>EF-translatable: flagged settings whose window overlaps [from, to].</summary>
    public static Expression<Func<EmployeeSetting, bool>> OverlapsRange(DateOnly from, DateOnly to) =>
        s => s.IsNoDTRNotRequired
             && (s.NoDtrEffectiveFrom == null || s.NoDtrEffectiveFrom <= to)
             && (s.NoDtrEffectiveUntil == null || s.NoDtrEffectiveUntil >= from);

    /// <summary>EF-translatable: flagged settings whose window covers every day of [from, to].</summary>
    public static Expression<Func<EmployeeSetting, bool>> CoversRange(DateOnly from, DateOnly to) =>
        s => s.IsNoDTRNotRequired
             && (s.NoDtrEffectiveFrom == null || s.NoDtrEffectiveFrom <= from)
             && (s.NoDtrEffectiveUntil == null || s.NoDtrEffectiveUntil >= to);

    /// <summary>Returns an error message, or null when the combination is valid (or the flag is off).</summary>
    public static string? Validate(bool flag, NoDtrCategory? category, DateOnly? from, DateOnly? until, SalaryType salaryType)
    {
        if (!flag) return null;
        if (salaryType != SalaryType.FIXED)
            return "Payroll-only (no DTR) employees must have a Fixed salary type";
        if (category == null)
            return "Select a category for a payroll-only (no DTR) employee";
        if (from != null && until != null && until < from)
            return "Effective until must be on or after effective from";
        return null;
    }
}
