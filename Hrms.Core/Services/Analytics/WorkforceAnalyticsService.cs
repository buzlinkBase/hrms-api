using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Analytics;

/// <summary>One employee's facts the workforce figures need.</summary>
public sealed record WorkforceMember(
    DateOnly HireDate,
    DateOnly? SeparationDate,
    EmploymentStatus EmploymentStatus,
    string Gender,
    DateOnly? BirthDate,
    string Department,
    string Branch,
    Guid? DepartmentId = null,
    Guid? BranchId = null)
{
    /// <summary>On the payroll that day: hired by then and not yet separated.</summary>
    public bool IsActiveOn(DateOnly date) => HireDate <= date && (SeparationDate == null || SeparationDate > date);
}

/// <summary>
/// Workforce analytics: headcount, hires, separations and turnover over time, and who's on
/// board by department, branch, status, tenure, age and gender. Headcount is counted from hire
/// and separation dates, so past months use each person's current department and branch.
/// </summary>
public class WorkforceAnalyticsService
{
    private readonly IUnitOfWorkService _uow;

    public WorkforceAnalyticsService(IUnitOfWorkService uow) => _uow = uow;

    public async Task<AnalyticsResult> GetAsync(AnalyticsFilter filter, CancellationToken token)
    {
        filter = AnalyticsPeriod.Normalize(filter);
        var members = await AnalyticsScope.Employees(_uow.Repository, filter)
            .Select(e => new
            {
                e.HireDate,
                e.DateResigned,
                e.EmploymentStatus,
                e.Gender,
                e.DOB,
                Department = e.Department != null ? e.Department.Name : null,
                Branch = e.Branch != null ? e.Branch.Name : null,
                e.DepartmentId,
                e.BranchId,
            })
            .ToListAsync(token);

        return Calculate(filter, members.Select(m => new WorkforceMember(
            m.HireDate,
            m.DateResigned is { } resigned ? DateOnly.FromDateTime(resigned) : null,
            m.EmploymentStatus,
            string.IsNullOrWhiteSpace(m.Gender) ? NotSet : m.Gender,
            m.DOB is { } dob ? DateOnly.FromDateTime(dob) : null,
            m.Department ?? AnalyticsScope.Unassigned,
            m.Branch ?? AnalyticsScope.Unassigned,
            m.Department != null ? m.DepartmentId : null,
            m.Branch != null ? m.BranchId : null)).ToList());
    }

    public static AnalyticsResult Calculate(AnalyticsFilter filter, IReadOnlyList<WorkforceMember> members)
    {
        var current = Totals(filter, members);
        var previous = Totals(AnalyticsPeriod.Previous(filter), members);

        var kpis = new List<AnalyticsKpi>
        {
            new("headcount", "Active headcount", current.Headcount, previous.Headcount, AnalyticsUnit.Count),
            new("hires", "Hires", current.Hires, previous.Hires, AnalyticsUnit.Count),
            new("separations", "Separations", current.Separations, previous.Separations, AnalyticsUnit.Count),
            new("turnover", "Turnover rate", current.Turnover, previous.Turnover, AnalyticsUnit.Percent),
            new("tenure", "Average tenure", current.AverageTenure, previous.AverageTenure, AnalyticsUnit.Years),
        };

        var series = AnalyticsPeriod.Months(filter.From, filter.To)
            .Select(month => new AnalyticsPoint(month.Label, new Dictionary<string, decimal>
            {
                ["headcount"] = members.Count(m => m.IsActiveOn(month.End)),
                ["hires"] = members.Count(m => m.HireDate >= month.Start && m.HireDate <= month.End),
                ["separations"] = members.Count(m => m.SeparationDate >= month.Start && m.SeparationDate <= month.End),
            }))
            .ToList();

        var active = members.Where(m => m.IsActiveOn(filter.To)).ToList();
        return new AnalyticsResult(
            filter.From,
            filter.To,
            kpis,
            [
                new("headcount", "Headcount", AnalyticsUnit.Count),
                new("hires", "Hires", AnalyticsUnit.Count),
                new("separations", "Separations", AnalyticsUnit.Count),
            ],
            series,
            [
                CountBy("department", "By department", active, m => new(m.Department, m.DepartmentId?.ToString())),
                CountBy("branch", "By branch", active, m => new(m.Branch, m.BranchId?.ToString())),
                CountBy("status", "By employment status", active, m => new(m.EmploymentStatus.ToString(), m.EmploymentStatus.ToString())),
                CountBy("tenure", "By tenure", active, m => TenureBand(m.HireDate, filter.To), TenureBands),
                CountBy("age", "By age", active, m => AgeBand(m.BirthDate, filter.To), AgeBands),
                CountBy("gender", "By gender", active, m => new(m.Gender, m.Gender == NotSet ? null : m.Gender)),
            ]);
    }

    private sealed record PeriodTotals(decimal Headcount, decimal Hires, decimal Separations, decimal Turnover, decimal AverageTenure);

    private static PeriodTotals Totals(AnalyticsFilter period, IReadOnlyList<WorkforceMember> members)
    {
        var monthEnds = AnalyticsPeriod.Months(period.From, period.To).Select(m => m.End).ToList();
        var averageHeadcount = monthEnds.Count == 0 ? 0m : (decimal)monthEnds.Average(end => members.Count(m => m.IsActiveOn(end)));
        var separations = members.Count(m => m.SeparationDate >= period.From && m.SeparationDate <= period.To);
        var active = members.Where(m => m.IsActiveOn(period.To)).ToList();

        return new PeriodTotals(
            active.Count,
            members.Count(m => m.HireDate >= period.From && m.HireDate <= period.To),
            separations,
            AnalyticsPeriod.Percent(separations, averageHeadcount),
            active.Count == 0 ? 0 : Math.Round((decimal)active.Average(m => YearsBetween(m.HireDate, period.To)), 1));
    }

    private static double YearsBetween(DateOnly from, DateOnly to) => (to.DayNumber - from.DayNumber) / 365.25;

    private const string NotSet = "Not set";

    /// <summary>A breakdown row's label and what the page drills into for it (see <see cref="AnalyticsRow.Key"/>).</summary>
    private sealed record RowGroup(string Label, string? Key);

    // Tenure keys are "{from}-{to}" in years ("10-" for the last band), which the page turns into
    // a hire-date range. The employee list can't filter by birth date, so age bands have no key.
    private static readonly RowGroup[] TenureBands =
    [
        new("Under 1 year", "0-1"), new("1–3 years", "1-3"), new("3–5 years", "3-5"),
        new("5–10 years", "5-10"), new("10+ years", "10-"),
    ];
    private static readonly RowGroup[] AgeBands =
    [
        new("Under 25", null), new("25–34", null), new("35–44", null),
        new("45–54", null), new("55+", null), new(NotSet, null),
    ];

    private static RowGroup TenureBand(DateOnly hireDate, DateOnly asOf) => YearsBetween(hireDate, asOf) switch
    {
        < 1 => TenureBands[0],
        < 3 => TenureBands[1],
        < 5 => TenureBands[2],
        < 10 => TenureBands[3],
        _ => TenureBands[4],
    };

    private static RowGroup AgeBand(DateOnly? birthDate, DateOnly asOf) => birthDate is not { } dob
        ? AgeBands[5]
        : YearsBetween(dob, asOf) switch
        {
            < 25 => AgeBands[0],
            < 35 => AgeBands[1],
            < 45 => AgeBands[2],
            < 55 => AgeBands[3],
            _ => AgeBands[4],
        };

    private static readonly IReadOnlyList<AnalyticsColumn> CountColumns =
    [
        new("count", "Employees", AnalyticsUnit.Count),
        new("share", "Share", AnalyticsUnit.Percent),
    ];

    // Named bands keep their natural order (and show empty bands); anything else sorts biggest first.
    private static AnalyticsBreakdown CountBy(
        string key, string title, IReadOnlyList<WorkforceMember> active, Func<WorkforceMember, RowGroup> groupOf,
        IReadOnlyList<RowGroup>? order = null)
    {
        // Grouped by key as well as label, so two departments sharing a name stay apart.
        var counts = active.GroupBy(groupOf).ToDictionary(g => g.Key, g => g.Count());
        var groups = order != null
            ? order.Where(group => counts.ContainsKey(group) || group.Label != NotSet).ToList()
            : counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key.Label).Select(c => c.Key).ToList();

        var rows = groups.Select(group =>
        {
            var count = counts.GetValueOrDefault(group);
            return new AnalyticsRow(group.Label, new Dictionary<string, decimal>
            {
                ["count"] = count,
                ["share"] = AnalyticsPeriod.Percent(count, active.Count),
            }, group.Key);
        }).ToList();
        return new AnalyticsBreakdown(key, title, CountColumns, rows);
    }
}
