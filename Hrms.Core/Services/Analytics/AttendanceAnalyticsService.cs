using Hrms.Domain.Entities;

namespace Hrms.Core.Services.Analytics;

/// <summary>Summed attendance for a group of DTR days (a month, a department, an employee).</summary>
public sealed record AttendanceTotals(
    int ScheduledDays,
    int AbsentDays,
    double LateMinutes,
    double UndertimeMinutes,
    double OvertimeHours)
{
    public static readonly AttendanceTotals Empty = new(0, 0, 0, 0, 0);

    /// <summary>Share of scheduled days actually worked.</summary>
    public decimal AttendanceRate =>
        AnalyticsPeriod.Percent(ScheduledDays - AbsentDays, ScheduledDays);

    public decimal LateHours => Math.Round((decimal)LateMinutes / 60, 1);
    public decimal UndertimeHours => Math.Round((decimal)UndertimeMinutes / 60, 1);
    public decimal OvertimeHoursRounded => Math.Round((decimal)OvertimeHours, 1);
}

/// <summary>
/// Attendance analytics from the DTR: attendance rate, lateness, undertime, absences and overtime
/// per month and department, and who's late or absent most. Only posted DTR days count -- ones
/// whose DTR batch was approved -- so drafts and declined batches never skew the figures.
/// A scheduled day is one the DTR expected work (worked or absent); rest days and holidays aren't.
/// </summary>
public class AttendanceAnalyticsService
{
    private const int TopCount = 10;
    private readonly IUnitOfWorkService _uow;

    public AttendanceAnalyticsService(IUnitOfWorkService uow) => _uow = uow;

    private IQueryable<DailyRecord> Days(AnalyticsFilter filter)
    {
        var employeeIds = AnalyticsScope.EmployeeIds(_uow.Repository, filter);
        return _uow.Repository.FindAll<DailyRecord>().AsNoTracking()
            .Where(x => x.Posted && x.WorkDate >= filter.From && x.WorkDate <= filter.To && employeeIds.Contains(x.EmployeeId));
    }

    public async Task<AnalyticsResult> GetAsync(AnalyticsFilter filter, CancellationToken token)
    {
        filter = AnalyticsPeriod.Normalize(filter);

        var current = await TotalsAsync(Days(filter), token);
        var previous = await TotalsAsync(Days(AnalyticsPeriod.Previous(filter)), token);

        var byMonth = await Days(filter)
            .GroupBy(x => new { x.WorkDate.Year, x.WorkDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                ScheduledDays = g.Count(x => x.AbsentCount > 0 || x.RegularNetHours > 0),
                AbsentDays = g.Count(x => x.AbsentCount > 0),
                LateMinutes = g.Sum(x => x.LateMinutes),
                UndertimeMinutes = g.Sum(x => x.UTMinutes),
                OvertimeHours = g.Sum(x => x.RegularOTHours + x.RestDayOTHours + x.LegalHolOTHours + x.SpecialHolOTHours + x.RestLegalDayOTHours + x.RestSpecialDayOTHours),
            })
            .ToListAsync(token);

        var byDepartment = await Days(filter)
            .GroupBy(x => x.DepartmentId)
            .Select(g => new
            {
                DepartmentId = g.Key,
                ScheduledDays = g.Count(x => x.AbsentCount > 0 || x.RegularNetHours > 0),
                AbsentDays = g.Count(x => x.AbsentCount > 0),
                LateMinutes = g.Sum(x => x.LateMinutes),
                UndertimeMinutes = g.Sum(x => x.UTMinutes),
                OvertimeHours = g.Sum(x => x.RegularOTHours + x.RestDayOTHours + x.LegalHolOTHours + x.SpecialHolOTHours + x.RestLegalDayOTHours + x.RestSpecialDayOTHours),
            })
            .ToListAsync(token);

        var byEmployee = await Days(filter)
            .GroupBy(x => new { x.EmployeeId, x.FullName })
            .Select(g => new
            {
                g.Key.FullName,
                AbsentDays = g.Count(x => x.AbsentCount > 0),
                LateMinutes = g.Sum(x => x.LateMinutes),
            })
            .ToListAsync(token);

        var departmentNames = await DepartmentNamesAsync(token);

        return Calculate(
            filter,
            current,
            previous,
            byMonth.ToDictionary(
                m => AnalyticsPeriod.Label(m.Year, m.Month),
                m => new AttendanceTotals(m.ScheduledDays, m.AbsentDays, m.LateMinutes, m.UndertimeMinutes, m.OvertimeHours)),
            byDepartment.Select(d => (
                d.DepartmentId is { } id && departmentNames.TryGetValue(id, out var name) ? name : AnalyticsScope.Unassigned,
                new AttendanceTotals(d.ScheduledDays, d.AbsentDays, d.LateMinutes, d.UndertimeMinutes, d.OvertimeHours))).ToList(),
            byEmployee.Select(e => (e.FullName ?? "—", e.LateMinutes, e.AbsentDays)).ToList());
    }

    private static async Task<AttendanceTotals> TotalsAsync(IQueryable<DailyRecord> days, CancellationToken token)
    {
        var totals = await days
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ScheduledDays = g.Count(x => x.AbsentCount > 0 || x.RegularNetHours > 0),
                AbsentDays = g.Count(x => x.AbsentCount > 0),
                LateMinutes = g.Sum(x => x.LateMinutes),
                UndertimeMinutes = g.Sum(x => x.UTMinutes),
                OvertimeHours = g.Sum(x => x.RegularOTHours + x.RestDayOTHours + x.LegalHolOTHours + x.SpecialHolOTHours + x.RestLegalDayOTHours + x.RestSpecialDayOTHours),
            })
            .FirstOrDefaultAsync(token);
        return totals == null
            ? AttendanceTotals.Empty
            : new AttendanceTotals(totals.ScheduledDays, totals.AbsentDays, totals.LateMinutes, totals.UndertimeMinutes, totals.OvertimeHours);
    }

    private Task<Dictionary<Guid, string>> DepartmentNamesAsync(CancellationToken token) =>
        _uow.Repository.FindAll<Department>().AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, token);

    public static AnalyticsResult Calculate(
        AnalyticsFilter filter,
        AttendanceTotals current,
        AttendanceTotals previous,
        IReadOnlyDictionary<string, AttendanceTotals> byMonth,
        IReadOnlyList<(string Department, AttendanceTotals Totals)> byDepartment,
        IReadOnlyList<(string Name, double LateMinutes, int AbsentDays)> byEmployee)
    {
        var kpis = new List<AnalyticsKpi>
        {
            new("attendanceRate", "Attendance rate", current.AttendanceRate, previous.ScheduledDays == 0 ? null : previous.AttendanceRate, AnalyticsUnit.Percent),
            new("lateHours", "Late hours", current.LateHours, previous.LateHours, AnalyticsUnit.Hours),
            new("undertimeHours", "Undertime hours", current.UndertimeHours, previous.UndertimeHours, AnalyticsUnit.Hours),
            new("absences", "Absences", current.AbsentDays, previous.AbsentDays, AnalyticsUnit.Days),
            new("overtimeHours", "Overtime hours", current.OvertimeHoursRounded, previous.OvertimeHoursRounded, AnalyticsUnit.Hours),
        };

        var series = AnalyticsPeriod.Months(filter.From, filter.To)
            .Select(month =>
            {
                var t = byMonth.GetValueOrDefault(month.Label) ?? AttendanceTotals.Empty;
                return new AnalyticsPoint(month.Label, new Dictionary<string, decimal>
                {
                    ["attendanceRate"] = t.AttendanceRate,
                    ["lateHours"] = t.LateHours,
                    ["undertimeHours"] = t.UndertimeHours,
                    ["absences"] = t.AbsentDays,
                    ["overtimeHours"] = t.OvertimeHoursRounded,
                });
            })
            .ToList();

        var departmentRows = byDepartment
            .GroupBy(d => d.Department)
            .Select(g =>
            {
                var t = g.Select(x => x.Totals).Aggregate(AttendanceTotals.Empty, (a, b) => new AttendanceTotals(
                    a.ScheduledDays + b.ScheduledDays, a.AbsentDays + b.AbsentDays, a.LateMinutes + b.LateMinutes,
                    a.UndertimeMinutes + b.UndertimeMinutes, a.OvertimeHours + b.OvertimeHours));
                return new AnalyticsRow(g.Key, new Dictionary<string, decimal>
                {
                    ["attendanceRate"] = t.AttendanceRate,
                    ["lateHours"] = t.LateHours,
                    ["undertimeHours"] = t.UndertimeHours,
                    ["absences"] = t.AbsentDays,
                    ["overtimeHours"] = t.OvertimeHoursRounded,
                });
            })
            .OrderBy(r => r.Values["attendanceRate"])
            .ToList();

        return new AnalyticsResult(
            filter.From,
            filter.To,
            kpis,
            [
                new("attendanceRate", "Attendance rate", AnalyticsUnit.Percent),
                new("lateHours", "Late hours", AnalyticsUnit.Hours),
                new("undertimeHours", "Undertime hours", AnalyticsUnit.Hours),
                new("absences", "Absences", AnalyticsUnit.Days),
                new("overtimeHours", "Overtime hours", AnalyticsUnit.Hours),
            ],
            series,
            [
                new AnalyticsBreakdown("department", "By department",
                [
                    new("attendanceRate", "Attendance rate", AnalyticsUnit.Percent),
                    new("lateHours", "Late hours", AnalyticsUnit.Hours),
                    new("undertimeHours", "Undertime hours", AnalyticsUnit.Hours),
                    new("absences", "Absences", AnalyticsUnit.Days),
                    new("overtimeHours", "Overtime hours", AnalyticsUnit.Hours),
                ], departmentRows),
                new AnalyticsBreakdown("mostLate", "Most late",
                    [new("lateHours", "Late hours", AnalyticsUnit.Hours)],
                    byEmployee.Where(e => e.LateMinutes > 0).OrderByDescending(e => e.LateMinutes).Take(TopCount)
                        .Select(e => new AnalyticsRow(e.Name, new Dictionary<string, decimal> { ["lateHours"] = Math.Round((decimal)e.LateMinutes / 60, 1) }))
                        .ToList()),
                new AnalyticsBreakdown("mostAbsent", "Most absent",
                    [new("absences", "Absences", AnalyticsUnit.Days)],
                    byEmployee.Where(e => e.AbsentDays > 0).OrderByDescending(e => e.AbsentDays).Take(TopCount)
                        .Select(e => new AnalyticsRow(e.Name, new Dictionary<string, decimal> { ["absences"] = e.AbsentDays }))
                        .ToList()),
            ]);
    }
}
