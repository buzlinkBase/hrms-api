using Hrms.Core.Services.Deployments;
using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>
/// What the builder needs besides the DTR rows themselves: names, break lengths, punches, the schedule and, when the company keeps them,
/// its client sites, posts, contracted manning and deployments (Setup &gt; Deployment).
/// </summary>
public sealed record PortalSourceData(
    IReadOnlyList<DailyRecord> Records,
    IReadOnlyDictionary<Guid, Client> Clients,
    IReadOnlyDictionary<Guid, CostCenters> Areas,
    IReadOnlyDictionary<Guid, Employee> Employees,
    IReadOnlyDictionary<Guid, double> BreakMinutesByShift,
    IReadOnlyList<Attendance> Punches,
    IReadOnlyList<ScheduledHeadcount> Scheduled,
    IReadOnlyList<ClientSite>? ClientSites = null,
    IReadOnlyDictionary<Guid, ClientPost>? Posts = null,
    IReadOnlyList<PostRequirement>? Requirements = null,
    IReadOnlyList<Deployment>? Deployments = null);

/// <summary>How many employees were scheduled to a site's shift on a day (from the work schedule plan).</summary>
public sealed record ScheduledHeadcount(Guid ClientId, Guid AreaId, DateOnly WorkDate, string ShiftName, int Count);

/// <summary>
/// Turns an approved DTR batch into the portal's rows. Pure: no database, no HTTP, so every mapping rule is testable.
/// <list type="bullet">
/// <item>Where a day is billed: the client and project site of the device the person punched on, when that differs from their home
/// assignment (relievers and augmentation); otherwise the DTR row's own client and site.</item>
/// <item>The portal's site is the company's client site for that client and project site (Setup &gt; Deployment), else the project site.</item>
/// <item>The post is the one the person was deployed to that day, else a default post per site.</item>
/// <item>Days not worked (absent, on leave, rest day) are sent too, so the client's manning and the billing attachments show them.</item>
/// </list>
/// </summary>
public static class ClientPortalPayloadBuilder
{
    private const int MaxPunchesPerRecord = 20;
    private static readonly TimeSpan PunchMargin = TimeSpan.FromHours(2);

    public static PortalPayload Build(PortalSourceData data)
    {
        var context = new BuildContext(data);
        var located = data.Records
            .Select(r => (Record: r, Place: context.Locate(r)))
            .Where(x => x.Place is not null && data.Clients.ContainsKey(x.Place.ClientId) && data.Areas.ContainsKey(x.Place.AreaId))
            .Select(x => (x.Record, Place: x.Place!))
            .ToList();

        var clients = located.Select(x => x.Place.ClientId).Distinct().Select(id => PortalPlaces.ClientRow(data.Clients[id])).ToList();

        var sites = located.Select(x => x.Place).DistinctBy(p => p.SiteExternalId)
            .Select(p => new PortalSiteRow(p.SiteExternalId, Ext(p.ClientId), p.SiteCode, p.SiteName))
            .ToList();

        var posts = located.Select(x => x.Place).DistinctBy(p => p.PostExternalId)
            .Select(p => new PortalPostRow(p.PostExternalId, p.SiteExternalId, p.PostCode, p.PostName, p.Position))
            .ToList();

        var guards = located
            .Select(x => (x.Record.EmployeeId, x.Place.ClientId, x.Record.FullName))
            .DistinctBy(g => g.EmployeeId)
            .Select(g => PortalPlaces.GuardRow(g.EmployeeId, g.ClientId, data.Employees.GetValueOrDefault(g.EmployeeId), g.FullName))
            .ToList();

        var records = located.Select(x => ToRow(x.Record, x.Place, data, context)).ToList();
        var schedules = Schedules(data, located.Select(x => x.Place).ToList(), located.Select(x => x.Record.WorkDate).ToList());

        return new PortalPayload(clients, sites, posts, guards, schedules, records);
    }

    /// <summary>
    /// Cuts a payload into bus messages of at most <see cref="ClientPortalAttendanceUpserted.MaxRecords"/> records. Each message carries only
    /// the clients, sites, posts, guards and schedules its own records use, so the portal can apply any of them on its own and in any order.
    /// Nothing to send means no messages.
    /// </summary>
    public static List<ClientPortalAttendanceUpserted> Split(PortalPayload payload, Guid tenantId, string batchCode, string reason, int maxRecords = ClientPortalAttendanceUpserted.MaxRecords)
    {
        var chunks = payload.Records.Chunk(maxRecords).ToList();
        var messages = new List<ClientPortalAttendanceUpserted>(chunks.Count);
        foreach (var records in chunks)
        {
            var clients = records.Select(r => r.ClientExternalId).ToHashSet();
            var sites = records.Select(r => r.SiteExternalId).ToHashSet();
            var posts = records.Select(r => r.PostExternalId).ToHashSet();
            var guards = records.Select(r => r.GuardExternalId).ToHashSet();
            messages.Add(new ClientPortalAttendanceUpserted
            {
                TenantId = tenantId,
                BatchCode = batchCode,
                Reason = reason,
                Part = messages.Count + 1,
                Parts = chunks.Count,
                Clients = payload.Clients.Where(c => clients.Contains(c.ExternalId)).ToList(),
                Sites = payload.Sites.Where(s => sites.Contains(s.ExternalId)).ToList(),
                Posts = payload.Posts.Where(p => posts.Contains(p.ExternalId)).ToList(),
                Guards = payload.Guards.Where(g => guards.Contains(g.ExternalId)).ToList(),
                Schedules = payload.Schedules.Where(s => posts.Contains(s.PostExternalId)).ToList(),
                Records = records.ToList(),
                OccurredAtUtc = DateTime.UtcNow,
            });
        }
        return messages;
    }

    // ---- where a day belongs -------------------------------------------------------------------------------------

    private sealed class BuildContext
    {
        private readonly PortalSourceData _data;
        private readonly PortalPlaces _places;

        public Dictionary<Guid, List<Attendance>> PunchesByEmployee { get; }

        public BuildContext(PortalSourceData data)
        {
            _data = data;
            _places = new PortalPlaces(data.Areas, data.ClientSites, data.Posts, data.Deployments);
            PunchesByEmployee = data.Punches
                .Where(p => p.EmployeeId is not null)
                .GroupBy(p => p.EmployeeId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.WorkDateTime).ToList());
        }

        public IReadOnlyList<Attendance> PunchesOf(DailyRecord r)
        {
            if (!PunchesByEmployee.TryGetValue(r.EmployeeId, out var all))
                return [];
            var from = Earliest(r.ShiftStartTime, r.StartTime) - PunchMargin;
            var to = Latest(r.ShiftEndTime, r.EndTime) + PunchMargin;
            return all.Where(p => p.WorkDateTime >= from && p.WorkDateTime <= to).Take(MaxPunchesPerRecord).ToList();
        }

        public PortalPlace? Locate(DailyRecord r)
        {
            var (clientId, areaId) = (r.ClientId, r.AreaId);

            // Punched on a device of another client or site (a reliever, augmentation): the day belongs where the person actually was.
            var located = PunchesOf(r)
                .Where(p => p.ClientId is not null && p.OperationAreaId is not null)
                .GroupBy(p => (Client: p.ClientId!.Value, Area: p.OperationAreaId!.Value))
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();
            if (located != default && _data.Clients.ContainsKey(located.Client) && _data.Areas.ContainsKey(located.Area))
                (clientId, areaId) = (located.Client, located.Area);

            if (clientId is not { } client || areaId is not { } area)
                return null;
            return _places.Resolve(r.EmployeeId, r.WorkDate, client, area);
        }
    }

    // ---- the rows ------------------------------------------------------------------------------------------------

    private static PortalDtrRow ToRow(DailyRecord r, PortalPlace place, PortalSourceData data, BuildContext context)
    {
        var breakMinutes = r.ShiftId is { } shift && data.BreakMinutesByShift.TryGetValue(shift, out var b) ? (int)Math.Round(b) : 0;
        var leaveHours = r.PaidLeaveHours + r.UnpaidLeaveHours;
        var leave = r.LeavesInfo?.FirstOrDefault();
        var worked = r.StartTime is not null || r.EndTime is not null;

        return new PortalDtrRow(
            ExternalId: Ext(r.Id),
            ClientExternalId: Ext(place.ClientId),
            SiteExternalId: place.SiteExternalId,
            PostExternalId: place.PostExternalId,
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
            Punches: PunchesFor(r, context),
            DayType: DayType(r.WorkTypeEnum),
            RestDayHours: Hours(r.RestDayHours),
            LegalHolidayHours: Hours(LegalHolidayHours(r)),
            SpecialHolidayHours: Hours(SpecialHolidayHours(r)),
            HolidayOvertimeHours: Hours(HolidayOvertimeHours(r)),
            Absent: !worked && (r.AbsentCount > 0 || r.WorkTypeEnum == WorkType.Absent),
            LeaveHours: Hours(leaveHours),
            LeaveType: leaveHours > 0 ? Trimmed(leave?.Name, 100) ?? LeaveName(r.WorkTypeEnum) : null,
            LeavePaid: r.PaidLeaveHours > 0 || r.WorkTypeEnum is WorkType.PaidLeave or WorkType.GovFundedLeave,
            ScheduledStart: r.ShiftStartTime == default ? null : r.ShiftStartTime,
            ScheduledEnd: r.ShiftEndTime == default ? null : r.ShiftEndTime,
            ShiftKind: IsNight(r) ? "Night" : "Day",
            DeploymentRole: place.Deployment?.Role.ToString(),
            EventTag: place.Deployment?.EventTag);
    }

    /// <summary>
    /// Required headcount per post and shift for the days sent: the contracted manning of real posts, else (for a site's default post) the
    /// number of people the work schedule put on the site's shift.
    /// </summary>
    private static List<PortalScheduleRow> Schedules(PortalSourceData data, IReadOnlyList<PortalPlace> places, IReadOnlyList<DateOnly> dates)
    {
        var rows = new List<PortalScheduleRow>();
        if (dates.Count == 0)
            return rows;
        var from = dates.Min();
        var to = dates.Max();

        var realPosts = places.Where(p => p.IsRealPost).Select(p => p.PostExternalId).ToHashSet();
        foreach (var requirement in data.Requirements ?? [])
        {
            var post = Ext(requirement.PostId);
            if (!realPosts.Contains(post))
                continue;
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (PostRequirementRules.AppliesOn(requirement, day))
                    rows.Add(new PortalScheduleRow(post, day, requirement.ShiftLabel.Trim(), requirement.RequiredHeadcount));
            }
        }

        var defaultPosts = places.Where(p => !p.IsRealPost).Select(p => (p.ClientId, p.AreaId, p.PostExternalId)).Distinct().ToList();
        foreach (var s in data.Scheduled.Where(s => !string.IsNullOrWhiteSpace(s.ShiftName)))
        {
            var post = defaultPosts.FirstOrDefault(p => p.ClientId == s.ClientId && p.AreaId == s.AreaId);
            if (post != default)
                rows.Add(new PortalScheduleRow(post.PostExternalId, s.WorkDate, s.ShiftName.Trim(), s.Count));
        }

        return rows.DistinctBy(r => (r.PostExternalId, r.WorkDate, r.ShiftName.ToUpperInvariant())).ToList();
    }

    /// <summary>Base hours on an ordinary day. Rest-day work counts here too (also sent apart as <c>RestDayHours</c>).</summary>
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
    public static double HolidayHours(DailyRecord r) => LegalHolidayHours(r) + SpecialHolidayHours(r);

    public static double LegalHolidayHours(DailyRecord r) => r.LegalHolHours + r.RestLegalDayHours + r.DoubleLegalHours + r.RestDoubleLegalHours;

    public static double SpecialHolidayHours(DailyRecord r) => r.SpecialHolHours + r.RestSpecialDayHours;

    /// <summary>The part of the overtime worked on a holiday.</summary>
    public static double HolidayOvertimeHours(DailyRecord r) =>
        r.LegalHolOTHours + r.SpecialHolOTHours + r.RestLegalDayOTHours + r.RestSpecialDayOTHours + r.DoubleLegalOTHours + r.RestDoubleLegalOTHours;

    public static string DayType(WorkType type) => type switch
    {
        WorkType.RestDay or WorkType.RestDayDuty => "RestDay",
        WorkType.LegalHoliday or WorkType.LegalHolidayDuty => "Legal",
        WorkType.SpecialNonWorkingHoliday or WorkType.SpecialHolidayDuty => "Special",
        WorkType.RestDayLegalHoliday or WorkType.RestDayLegalHolidayDuty => "RestLegal",
        WorkType.RestDaySpecialHoliday or WorkType.RestDaySpecialHolidayDuty => "RestSpecial",
        WorkType.DoubleLegal or WorkType.DoubleLegalDuty => "DoubleLegal",
        WorkType.RestDayDoubleLegal or WorkType.RestDayDoubleLegalDuty => "RestDoubleLegal",
        _ => "Regular",
    };

    private static string? LeaveName(WorkType type) => type switch
    {
        WorkType.PaidLeave => "Paid leave",
        WorkType.UnpaidLeave => "Unpaid leave",
        WorkType.GovFundedLeave => "Government-funded leave",
        _ => "Leave",
    };

    /// <summary>A night shift: named so, or starting in the evening or before dawn.</summary>
    public static bool IsNight(DailyRecord r)
    {
        if (!string.IsNullOrWhiteSpace(r.ShiftName) && r.ShiftName.Contains("night", StringComparison.OrdinalIgnoreCase))
            return true;
        var start = r.ShiftStartTime != default ? r.ShiftStartTime : r.StartTime ?? default;
        return start != default && (start.Hour >= 17 || start.Hour < 4);
    }

    private static List<PortalPunchRow> PunchesFor(DailyRecord r, BuildContext context)
    {
        var inWindow = context.PunchesOf(r);
        if (inWindow.Count > 0)
            return inWindow.Select(p => new PortalPunchRow(p.WorkDateTime, "punch")).ToList();

        // Entered or corrected by hand, so no device punches exist: the recorded in/out are the evidence.
        var synthetic = new List<PortalPunchRow>();
        if (r.StartTime is { } start) synthetic.Add(new PortalPunchRow(start, "in"));
        if (r.EndTime is { } end) synthetic.Add(new PortalPunchRow(end, "out"));
        return synthetic;
    }

    private static DateTime Earliest(DateTime shift, DateTime? actual) => actual is { } a && a < shift ? a : shift;

    private static DateTime Latest(DateTime shift, DateTime? actual) => actual is { } a && a > shift ? a : shift;

    private static int Minutes(double value) => value <= 0 ? 0 : (int)Math.Round(value);

    /// <summary>Four decimals: hours come from minutes (1 min = 0.0167 h), and billing must reproduce them to the centavo.</summary>
    private static decimal Hours(double value) => value <= 0 ? 0m : Math.Round((decimal)value, 4);

    private static string Ext(Guid id) => PortalPlaces.Ext(id);

    private static string? Trimmed(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
}
