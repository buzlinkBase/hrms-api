using Hrms.Domain.Entities;

namespace Hrms.Core.Services.Analytics;

/// <summary>Summed pay for a group of payroll rows (a month, a department, a run type).</summary>
public sealed record PayrollTotals(decimal Gross, decimal Net, decimal EmployerContributions, decimal Overtime, int Payslips)
{
    public static readonly PayrollTotals Empty = new(0, 0, 0, 0, 0);

    /// <summary>What the company spends: gross pay plus its own SSS/PhilHealth/Pag-IBIG/EC share.</summary>
    public decimal LaborCost => Gross + EmployerContributions;

    public decimal OvertimeShare => AnalyticsPeriod.Percent(Overtime, Gross);

    public PayrollTotals Plus(PayrollTotals other) => new(
        Gross + other.Gross, Net + other.Net, EmployerContributions + other.EmployerContributions,
        Overtime + other.Overtime, Payslips + other.Payslips);
}

/// <summary>
/// Payroll and labor-cost analytics from approved payroll runs only (drafts and declined runs
/// never count), by the month each pay period ends. Pay rows are confidential-class, so the
/// normal query filter already leaves out confidential employees for anyone not cleared --
/// the page shows the usual notice when that happens.
/// </summary>
public class PayrollAnalyticsService
{
    private readonly IUnitOfWorkService _uow;

    public PayrollAnalyticsService(IUnitOfWorkService uow) => _uow = uow;

    private IQueryable<Payroll> Rows(AnalyticsFilter filter)
    {
        var employeeIds = AnalyticsScope.EmployeeIds(_uow.Repository, filter);
        return _uow.Repository.FindAll<Payroll>().AsNoTracking()
            .Where(x => x.ApprovalStatus == ApprovalStatus.Approved &&
                        x.PayPeriodEnd >= filter.From && x.PayPeriodEnd <= filter.To &&
                        employeeIds.Contains(x.EmployeeId));
    }

    public async Task<AnalyticsResult> GetAsync(AnalyticsFilter filter, CancellationToken token)
    {
        filter = AnalyticsPeriod.Normalize(filter);

        var byMonth = await Rows(filter)
            .GroupBy(x => new { x.PayPeriodEnd.Year, x.PayPeriodEnd.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Gross = g.Sum(x => x.GrossPay),
                Net = g.Sum(x => x.NetPay),
                Employer = g.Sum(x => x.EmployerSSSContribution + x.EmployerPhilHealthContribution + x.EmployerPagIbigContribution + x.EmployerECContribution),
                Overtime = g.Sum(x => x.OvertimePay),
                Payslips = g.Count(),
            })
            .ToListAsync(token);

        // A payroll row keeps no department, so this is each employee's current one.
        var byDepartment = await Rows(filter)
            .Join(AnalyticsScope.Employees(_uow.Repository, filter), p => p.EmployeeId, e => e.Id,
                (p, e) => new { e.DepartmentId, p.GrossPay, p.NetPay, p.OvertimePay,
                    Employer = p.EmployerSSSContribution + p.EmployerPhilHealthContribution + p.EmployerPagIbigContribution + p.EmployerECContribution })
            .GroupBy(x => x.DepartmentId)
            .Select(g => new
            {
                DepartmentId = g.Key,
                Gross = g.Sum(x => x.GrossPay),
                Net = g.Sum(x => x.NetPay),
                Employer = g.Sum(x => x.Employer),
                Overtime = g.Sum(x => x.OvertimePay),
                Payslips = g.Count(),
            })
            .ToListAsync(token);

        var byRunType = await Rows(filter)
            .GroupBy(x => x.PayrollType)
            .Select(g => new
            {
                RunType = g.Key,
                Gross = g.Sum(x => x.GrossPay),
                Net = g.Sum(x => x.NetPay),
                Employer = g.Sum(x => x.EmployerSSSContribution + x.EmployerPhilHealthContribution + x.EmployerPagIbigContribution + x.EmployerECContribution),
                Overtime = g.Sum(x => x.OvertimePay),
                Payslips = g.Count(),
            })
            .ToListAsync(token);

        var previous = await Rows(AnalyticsPeriod.Previous(filter))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Gross = g.Sum(x => x.GrossPay),
                Net = g.Sum(x => x.NetPay),
                Employer = g.Sum(x => x.EmployerSSSContribution + x.EmployerPhilHealthContribution + x.EmployerPagIbigContribution + x.EmployerECContribution),
                Overtime = g.Sum(x => x.OvertimePay),
                Payslips = g.Count(),
            })
            .FirstOrDefaultAsync(token);

        var departmentNames = await _uow.Repository.FindAll<Department>().AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.Name, token);

        return Calculate(
            filter,
            previous == null ? PayrollTotals.Empty : new PayrollTotals(previous.Gross, previous.Net, previous.Employer, previous.Overtime, previous.Payslips),
            byMonth.ToDictionary(
                m => AnalyticsPeriod.Label(m.Year, m.Month),
                m => new PayrollTotals(m.Gross, m.Net, m.Employer, m.Overtime, m.Payslips)),
            byDepartment.Select(d =>
            {
                var totals = new PayrollTotals(d.Gross, d.Net, d.Employer, d.Overtime, d.Payslips);
                return d.DepartmentId is { } id && departmentNames.TryGetValue(id, out var name)
                    ? (name, (string?)id.ToString(), totals)
                    : (AnalyticsScope.Unassigned, null, totals);
            }).ToList(),
            byRunType.Select(r => (RunTypeLabel(r.RunType), (string?)r.RunType.ToString(), new PayrollTotals(r.Gross, r.Net, r.Employer, r.Overtime, r.Payslips))).ToList());
    }

    public static string RunTypeLabel(PayrollType type) => type switch
    {
        PayrollType.ThirteenthMonth => "13th Month",
        PayrollType.LastPay => "Last Pay",
        PayrollType.YearEndAdjustment => "Year-End Adjustment",
        _ => "Regular",
    };

    private static readonly IReadOnlyList<AnalyticsColumn> CostColumns =
    [
        new("laborCost", "Labor cost", AnalyticsUnit.Currency),
        new("gross", "Gross pay", AnalyticsUnit.Currency),
        new("net", "Net pay", AnalyticsUnit.Currency),
        new("employerContributions", "Employer contributions", AnalyticsUnit.Currency),
        new("overtime", "Overtime pay", AnalyticsUnit.Currency),
        new("payslips", "Payslips", AnalyticsUnit.Count),
    ];

    private static Dictionary<string, decimal> Values(PayrollTotals t) => new()
    {
        ["laborCost"] = t.LaborCost,
        ["gross"] = t.Gross,
        ["net"] = t.Net,
        ["employerContributions"] = t.EmployerContributions,
        ["overtime"] = t.Overtime,
        ["payslips"] = t.Payslips,
    };

    // By key as well as label, so two departments sharing a name stay apart.
    private static List<AnalyticsRow> BreakdownRows(IEnumerable<(string Label, string? Key, PayrollTotals Totals)> groups) => groups
        .GroupBy(g => (g.Label, g.Key))
        .Select(g => (g.Key.Label, g.Key.Key, Totals: g.Select(x => x.Totals).Aggregate(PayrollTotals.Empty, (a, b) => a.Plus(b))))
        .OrderByDescending(g => g.Totals.LaborCost)
        .Select(g => new AnalyticsRow(g.Label, Values(g.Totals), g.Key))
        .ToList();

    public static AnalyticsResult Calculate(
        AnalyticsFilter filter,
        PayrollTotals previous,
        IReadOnlyDictionary<string, PayrollTotals> byMonth,
        IReadOnlyList<(string Department, string? DepartmentId, PayrollTotals Totals)> byDepartment,
        IReadOnlyList<(string RunType, string? RunTypeKey, PayrollTotals Totals)> byRunType)
    {
        var current = byMonth.Values.Aggregate(PayrollTotals.Empty, (a, b) => a.Plus(b));
        var kpis = new List<AnalyticsKpi>
        {
            new("laborCost", "Total labor cost", current.LaborCost, previous.LaborCost, AnalyticsUnit.Currency),
            new("gross", "Gross pay", current.Gross, previous.Gross, AnalyticsUnit.Currency),
            new("net", "Net pay", current.Net, previous.Net, AnalyticsUnit.Currency),
            new("employerContributions", "Employer contributions", current.EmployerContributions, previous.EmployerContributions, AnalyticsUnit.Currency),
            new("overtimeShare", "Overtime share of gross", current.OvertimeShare, previous.Gross == 0 ? null : previous.OvertimeShare, AnalyticsUnit.Percent),
        };

        var series = AnalyticsPeriod.Months(filter.From, filter.To)
            .Select(month =>
            {
                var t = byMonth.GetValueOrDefault(month.Label) ?? PayrollTotals.Empty;
                return new AnalyticsPoint(month.Label, new Dictionary<string, decimal>
                {
                    ["gross"] = t.Gross,
                    ["net"] = t.Net,
                    ["employerContributions"] = t.EmployerContributions,
                    ["overtime"] = t.Overtime,
                    ["laborCost"] = t.LaborCost,
                });
            })
            .ToList();

        return new AnalyticsResult(
            filter.From,
            filter.To,
            kpis,
            [
                new("laborCost", "Labor cost", AnalyticsUnit.Currency),
                new("gross", "Gross pay", AnalyticsUnit.Currency),
                new("net", "Net pay", AnalyticsUnit.Currency),
                new("employerContributions", "Employer contributions", AnalyticsUnit.Currency),
                new("overtime", "Overtime pay", AnalyticsUnit.Currency),
            ],
            series,
            [
                new AnalyticsBreakdown("department", "By department", CostColumns, BreakdownRows(byDepartment)),
                new AnalyticsBreakdown("runType", "By run type", CostColumns, BreakdownRows(byRunType)),
            ]);
    }
}
