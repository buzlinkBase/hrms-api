using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

// Current-snapshot splits for the Dashboard charts. Counts employees who haven't resigned, read
// through the normal (tenant- and confidentiality-filtered) queries.
public class DashboardWorkforceService
{
    private const int MaxBars = 8;
    private const string NoBranch = "No branch";
    private const string NoSite = "No site";

    // Upper bound (exclusive, in whole years) of each band; the last band is open-ended.
    private static readonly (string Label, int UpToYears)[] TenureBands =
    [
        ("Under 1 year", 1),
        ("1 to 3 years", 3),
        ("3 to 5 years", 5),
        ("5 to 10 years", 10),
        ("10+ years", int.MaxValue),
    ];

    private readonly IUnitOfWorkService _uow;

    public DashboardWorkforceService(IUnitOfWorkService uow) => _uow = uow;

    public async Task<DashboardWorkforceMix> GetMixAsync(DateOnly today, CancellationToken token)
    {
        var current = _uow.Repository.Find<Employee>(e => e.DateResigned == null).AsNoTracking();

        var status = await current
            .GroupBy(e => e.EmploymentStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(token);

        var branches = await TopCountsAsync(current.Select(e => e.Branch != null ? e.Branch.Name : NoBranch), token);

        // A project site is the employee's Area (CostCenters).
        var sites = await TopCountsAsync(current.Select(e => e.Area != null ? e.Area.Name : NoSite), token);

        var hireDates = await current.Select(e => e.HireDate).ToListAsync(token);

        return new DashboardWorkforceMix
        {
            EmploymentStatus = status
                .OrderByDescending(x => x.Count)
                .Select(x => new DashboardCount { Label = x.Status.ToString(), Count = x.Count })
                .ToList(),
            Branches = branches,
            Sites = sites,
            Tenure = TenureCounts(hireDates, today),
        };
    }

    // The largest groups of a label, biggest first.
    private static Task<List<DashboardCount>> TopCountsAsync(IQueryable<string> labels, CancellationToken token) =>
        labels
            .GroupBy(label => label)
            .Select(g => new DashboardCount { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(MaxBars)
            .ToListAsync(token);

    // A hire date in the future counts as under a year.
    public static List<DashboardCount> TenureCounts(IEnumerable<DateOnly> hireDates, DateOnly today)
    {
        var counts = new int[TenureBands.Length];
        foreach (var hired in hireDates)
        {
            var years = YearsBetween(hired, today);
            var band = Array.FindIndex(TenureBands, b => years < b.UpToYears);
            counts[band]++;
        }
        return TenureBands
            .Select((b, i) => new DashboardCount { Label = b.Label, Count = counts[i] })
            .ToList();
    }

    private static int YearsBetween(DateOnly from, DateOnly to)
    {
        if (from >= to) return 0;
        var years = to.Year - from.Year;
        if (to < from.AddYears(years)) years--;
        return years;
    }
}
