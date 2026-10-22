using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Analytics;

/// <summary>What an Analytics page asks for. Dates are inclusive; everything else optional.</summary>
public sealed record AnalyticsFilter
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public EmploymentStatus? EmploymentStatus { get; set; }
}

/// <summary>How a figure should be read and formatted by the page.</summary>
public enum AnalyticsUnit
{
    Count,
    Percent,
    Hours,
    Days,
    Currency,
    Years,
}

/// <param name="Previous">The same figure for the period of equal length just before, for the "vs previous" change.</param>
public sealed record AnalyticsKpi(string Key, string Label, decimal Value, decimal? Previous, AnalyticsUnit Unit);

/// <summary>One point of a trend: its period ("2026-09") and a value per series key.</summary>
public sealed record AnalyticsPoint(string Period, IReadOnlyDictionary<string, decimal> Values);

public sealed record AnalyticsSeries(string Key, string Label, AnalyticsUnit Unit);

/// <param name="Key">What the page filters by when it drills into the row -- an id, an enum name or a
/// band code, depending on the breakdown. Null when the row has nothing to drill into.</param>
public sealed record AnalyticsRow(string Label, IReadOnlyDictionary<string, decimal> Values, string? Key = null);

public sealed record AnalyticsColumn(string Key, string Label, AnalyticsUnit Unit);

public sealed record AnalyticsBreakdown(string Key, string Title, IReadOnlyList<AnalyticsColumn> Columns, IReadOnlyList<AnalyticsRow> Rows);

/// <summary>One Analytics page's data: headline figures, a monthly trend, and breakdown tables.</summary>
public sealed record AnalyticsResult(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AnalyticsKpi> Kpis,
    IReadOnlyList<AnalyticsSeries> SeriesDefinitions,
    IReadOnlyList<AnalyticsPoint> Series,
    IReadOnlyList<AnalyticsBreakdown> Breakdowns);

public static class AnalyticsPeriod
{
    public const int MaxMonths = 24;

    /// <summary>Never backwards, and never longer than <see cref="MaxMonths"/> (the start moves up).</summary>
    public static AnalyticsFilter Normalize(AnalyticsFilter filter)
    {
        var (from, to) = filter.From <= filter.To ? (filter.From, filter.To) : (filter.To, filter.From);
        var earliest = to.AddMonths(-MaxMonths).AddDays(1);
        return filter with { From = from < earliest ? earliest : from, To = to };
    }

    /// <summary>The period of the same length that ends the day before <paramref name="filter"/> starts.</summary>
    public static AnalyticsFilter Previous(AnalyticsFilter filter)
    {
        var days = filter.To.DayNumber - filter.From.DayNumber;
        var to = filter.From.AddDays(-1);
        return filter with { From = to.AddDays(-days), To = to };
    }

    /// <summary>Each calendar month the range touches, as (label, first day, last day clipped to the range).</summary>
    public static IEnumerable<(string Label, DateOnly Start, DateOnly End)> Months(DateOnly from, DateOnly to)
    {
        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            var start = month < from ? from : month;
            var end = month.AddMonths(1).AddDays(-1);
            yield return (Label(month.Year, month.Month), start, end > to ? to : end);
        }
    }

    public static string Label(int year, int month) => $"{year:D4}-{month:D2}";

    public static decimal Percent(decimal part, decimal whole) =>
        whole == 0 ? 0 : Math.Round(part / whole * 100, 1);
}
