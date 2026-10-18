using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>What the builder needs besides the DTR rows themselves: names, break lengths, punches and the schedule.</summary>
public sealed record PortalSourceData(
    IReadOnlyList<DailyRecord> Records,
    IReadOnlyDictionary<Guid, Client> Clients,
    IReadOnlyDictionary<Guid, CostCenters> Areas,
    IReadOnlyDictionary<Guid, Employee> Employees,
    IReadOnlyDictionary<Guid, double> BreakMinutesByShift,
    IReadOnlyList<Attendance> Punches,
    IReadOnlyList<ScheduledHeadcount> Scheduled);

/// <summary>How many employees were scheduled to a site's shift on a day (from the work schedule plan).</summary>
public sealed record ScheduledHeadcount(Guid ClientId, Guid AreaId, DateOnly WorkDate, string ShiftName, int Count);

/// <summary>
/// Turns an approved DTR batch into the portal's rows. Pure: no database, no HTTP, so every mapping rule is testable.
/// A client's "site" is an area (project site) and each site has one default post, because HRIS has no post concept.
/// </summary>
public static class ClientPortalPayloadBuilder
{
    private const int MaxPunchesPerRecord = 20;
    private static readonly TimeSpan PunchMargin = TimeSpan.FromHours(2);

    public static PortalPayload Build(PortalSourceData data)
    {
        // A day with no time in and no time out is an absence or leave: nothing to review or bill.
        var worked = data.Records
            .Where(r => r.ClientId is not null && r.AreaId is not null && (r.StartTime is not null || r.EndTime is not null))
            .ToList();

        var clientIds = worked.Select(r => r.ClientId!.Value).Distinct().ToList();
        var areaKeys = worked.Select(r => (Client: r.ClientId!.Value, Area: r.AreaId!.Value)).Distinct().ToList();

        var clients = clientIds
            .Where(data.Clients.ContainsKey)
            .Select(id => new PortalClientRow(Ext(id), Code(data.Clients[id].Code, id), Name(data.Clients[id].Name, "Client")))
            .ToList();

        var sites = areaKeys
            .Where(k => data.Areas.ContainsKey(k.Area) && data.Clients.ContainsKey(k.Client))
            .Select(k => new PortalSiteRow(Ext(k.Area), Ext(k.Client), Code(data.Areas[k.Area].Code, k.Area), Name(data.Areas[k.Area].Name, "Site")))
            .DistinctBy(s => s.ExternalId)
            .ToList();

        var posts = sites
            .Select(s => new PortalPostRow(PostExt(s.ExternalId), s.ExternalId, "MAIN", s.Name))
            .ToList();

        var guards = worked
            .Select(r => (r.EmployeeId, Client: r.ClientId!.Value, r.FullName))
            .DistinctBy(g => g.EmployeeId)
            .Where(g => data.Clients.ContainsKey(g.Client))
            .Select(g =>
            {
                data.Employees.TryGetValue(g.EmployeeId, out var employee);
                var name = !string.IsNullOrWhiteSpace(g.FullName) ? g.FullName! : employee is null ? "Guard" : $"{employee.LastName}, {employee.FirstName}".Trim(' ', ',');
                return new PortalGuardRow(Ext(g.EmployeeId), Ext(g.Client), Code(employee?.EmployeeNo, g.EmployeeId), name);
            })
            .ToList();

        var knownSites = sites.Select(s => s.ExternalId).ToHashSet();
        var schedules = data.Scheduled
            .Where(s => knownSites.Contains(Ext(s.AreaId)) && !string.IsNullOrWhiteSpace(s.ShiftName))
            .Select(s => new PortalScheduleRow(PostExt(Ext(s.AreaId)), s.WorkDate, s.ShiftName.Trim(), s.Count))
            .ToList();

        var punchesByEmployee = data.Punches
            .Where(p => p.EmployeeId is not null)
            .GroupBy(p => p.EmployeeId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.WorkDateTime).ToList());

        var records = worked
            .Where(r => data.Clients.ContainsKey(r.ClientId!.Value) && data.Areas.ContainsKey(r.AreaId!.Value))
            .Select(r => ToRow(r, data, punchesByEmployee))
            .ToList();

        return new PortalPayload(clients, sites, posts, guards, schedules, records);
    }

    private static PortalDtrRow ToRow(DailyRecord r, PortalSourceData data, Dictionary<Guid, List<Attendance>> punchesByEmployee)
    {
        var siteExt = Ext(r.AreaId!.Value);
        var breakMinutes = r.ShiftId is { } shift && data.BreakMinutesByShift.TryGetValue(shift, out var b) ? (int)Math.Round(b) : 0;

        return new PortalDtrRow(
            ExternalId: Ext(r.Id),
            ClientExternalId: Ext(r.ClientId!.Value),
            SiteExternalId: siteExt,
            PostExternalId: PostExt(siteExt),
            GuardExternalId: Ext(r.EmployeeId),
            WorkDate: r.WorkDate,
            ShiftName: string.IsNullOrWhiteSpace(r.ShiftName) ? "Shift" : r.ShiftName.Trim(),
            TimeIn: r.StartTime,
            TimeOut: r.EndTime,
            BreakMinutes: breakMinutes,
            LateMinutes: Minutes(r.LateMinutes),
            UndertimeMinutes: Minutes(r.UTMinutes),
            RegularHours: Hours(RegularHours(r)),
            OvertimeHours: Hours(OvertimeHours(r)),
            NightDiffHours: Hours(NightDiffHours(r)),
            HolidayHours: Hours(HolidayHours(r)),
            // The batch only reaches the portal once it is approved, and approval covers the overtime in it.
            OvertimeApproved: true,
            Punches: PunchesFor(r, punchesByEmployee));
    }

    /// <summary>Base hours on an ordinary day. Rest-day work counts here too: the portal has no separate rest-day bucket.</summary>
    public static double RegularHours(DailyRecord r) => r.RegularNetHours + r.RestDayHours;

    public static double OvertimeHours(DailyRecord r) =>
        r.RegularOTHours + r.RestDayOTHours + r.LegalHolOTHours + r.SpecialHolOTHours
        + r.RestLegalDayOTHours + r.RestSpecialDayOTHours + r.DoubleLegalOTHours + r.RestDoubleLegalOTHours;

    /// <summary>Night-differential hours, including those worked as overtime. They are a subset of the hours above, not extra hours.</summary>
    public static double NightDiffHours(DailyRecord r) =>
        r.RegularNDHours + r.RegularNDOTHours
        + r.RestDayNDHours + r.RestDayNDOTHours
        + r.LegalHolNightDiffHours + r.LegalHolNightDiffOTHours
        + r.SpecialHolNightDiffHours + r.SpecialHolNightDiffOTHours
        + r.RestLegalDayNDHours + r.RestLegalDayNDOTHours
        + r.RestSpecialDayNDHours + r.RestSpecialDayNDOTHours
        + r.DoubleLegalNDHours + r.DoubleLegalNDOTHours
        + r.RestDoubleLegalNDHours + r.RestDoubleLegalNDOTHours;

    /// <summary>Base hours worked on a legal or special holiday, alone or combined with a rest day.</summary>
    public static double HolidayHours(DailyRecord r) =>
        r.LegalHolHours + r.SpecialHolHours + r.RestLegalDayHours + r.RestSpecialDayHours + r.DoubleLegalHours + r.RestDoubleLegalHours;

    private static List<PortalPunchRow> PunchesFor(DailyRecord r, Dictionary<Guid, List<Attendance>> punchesByEmployee)
    {
        if (punchesByEmployee.TryGetValue(r.EmployeeId, out var all))
        {
            var from = Earliest(r.ShiftStartTime, r.StartTime) - PunchMargin;
            var to = Latest(r.ShiftEndTime, r.EndTime) + PunchMargin;
            var inWindow = all.Where(p => p.WorkDateTime >= from && p.WorkDateTime <= to).Take(MaxPunchesPerRecord).ToList();
            if (inWindow.Count > 0)
                return inWindow.Select(p => new PortalPunchRow(p.WorkDateTime, "punch")).ToList();
        }

        // Entered or corrected by hand, so no device punches exist: the recorded in/out are the evidence.
        var synthetic = new List<PortalPunchRow>();
        if (r.StartTime is { } start) synthetic.Add(new PortalPunchRow(start, "in"));
        if (r.EndTime is { } end) synthetic.Add(new PortalPunchRow(end, "out"));
        return synthetic;
    }

    private static DateTime Earliest(DateTime shift, DateTime? actual) => actual is { } a && a < shift ? a : shift;

    private static DateTime Latest(DateTime shift, DateTime? actual) => actual is { } a && a > shift ? a : shift;

    private static int Minutes(double value) => value <= 0 ? 0 : (int)Math.Round(value);

    private static decimal Hours(double value) => value <= 0 ? 0m : Math.Round((decimal)value, 2);

    private static string Ext(Guid id) => id.ToString("D");

    private static string PostExt(string siteExternalId) => $"post-{siteExternalId}";

    private static string Name(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    // The portal requires a code; fall back to a short slice of the id when HRIS has none.
    private static string Code(string? value, Guid id) => string.IsNullOrWhiteSpace(value) ? id.ToString("N")[..8].ToUpperInvariant() : value.Trim();
}
