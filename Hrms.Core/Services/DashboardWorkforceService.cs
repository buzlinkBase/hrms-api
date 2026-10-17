using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

// Current-snapshot splits for the Dashboard charts. Counts employees who haven't resigned, read
// through the normal (tenant- and confidentiality-filtered) queries.
public class DashboardWorkforceService
{
    private const int MaxBranches = 8;
    private const string NoBranch = "No branch";

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

        var branches = await current
            .GroupBy(e => e.Branch != null ? e.Branch.Name : NoBranch)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(MaxBranches)
            .ToListAsync(token);

        var hireDates = await current.Select(e => e.HireDate).ToListAsync(token);

        return new DashboardWorkforceMix
        {
            EmploymentStatus = status
                .OrderByDescending(x => x.Count)
                .Select(x => new DashboardCount { Label = x.Status.ToString(), Count = x.Count })
                .ToList(),
            Branches = branches
                .Select(x => new DashboardCount { Label = x.Label, Count = x.Count })
                .ToList(),
            Tenure = TenureCounts(hireDates, today),
        };
    }

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
