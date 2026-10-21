using Hrms.Core.Services.Deployments;
using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>What the live punch feed needs: the punches just saved and the names and deployment setup to place them.</summary>
public sealed record LivePunchSource(
    IReadOnlyList<Attendance> Punches,
    IReadOnlyDictionary<Guid, Client> Clients,
    IReadOnlyDictionary<Guid, CostCenters> Areas,
    IReadOnlyDictionary<Guid, Employee> Employees,
    IReadOnlyList<ClientSite>? Sites = null,
    IReadOnlyDictionary<Guid, ClientPost>? Posts = null,
    IReadOnlyList<Deployment>? Deployments = null);

/// <summary>What the manning feed needs: a company's client sites, posts, contracted manning, deployments and shift times.</summary>
public sealed record ManningSource(
    IReadOnlyDictionary<Guid, Client> Clients,
    IReadOnlyList<ClientSite> Sites,
    IReadOnlyDictionary<Guid, ClientPost> Posts,
    IReadOnlyList<PostRequirement> Requirements,
    IReadOnlyList<Deployment> Deployments,
    IReadOnlyDictionary<Guid, Employee> Employees,
    IReadOnlyDictionary<Guid, (TimeSpan Start, TimeSpan End)> ShiftTimes);

/// <summary>
/// Builds the live feed to the Client Portal: punches as they arrive, and the manning planned for the coming days. Pure, so every rule
/// is testable. Places follow <see cref="PortalPlaces"/>, the same as the DTR feed.
/// </summary>
public static class ClientPortalLiveBuilder
{
    /// <summary>Punches older than this (an upload of last month's logs) are not live and are left to the DTR batches.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(36);

    /// <summary>Clock skew allowed for punches stamped a little ahead of the server.</summary>
    private static readonly TimeSpan MaxAhead = TimeSpan.FromMinutes(15);

    public static List<ClientPortalPunchesRecorded> Punches(LivePunchSource source, Guid tenantId, DateTime nowLocal)
    {
        var places = new PortalPlaces(source.Areas, source.Sites, source.Posts, source.Deployments);
        var located = source.Punches
            .Where(p => p.EmployeeId is not null && p.ClientId is { } c && p.OperationAreaId is { } a && source.Clients.ContainsKey(c) && source.Areas.ContainsKey(a))
            .Where(p => p.WorkDateTime >= nowLocal - MaxAge && p.WorkDateTime <= nowLocal + MaxAhead)
            .Select(p => (Punch: p, Place: places.Resolve(p.EmployeeId!.Value, DateOnly.FromDateTime(p.WorkDateTime), p.ClientId!.Value, p.OperationAreaId!.Value)))
            .OrderBy(x => x.Punch.WorkDateTime)
            .ToList();

        var messages = new List<ClientPortalPunchesRecorded>();
        foreach (var chunk in located.Chunk(ClientPortalPunchesRecorded.MaxPunches))
        {
            messages.Add(new ClientPortalPunchesRecorded
            {
                TenantId = tenantId,
                Clients = chunk.Select(x => x.Place.ClientId).Distinct().Select(id => PortalPlaces.ClientRow(source.Clients[id])).ToList(),
                Sites = chunk.Select(x => x.Place).DistinctBy(p => p.SiteExternalId)
                    .Select(p => new PortalSiteRow(p.SiteExternalId, PortalPlaces.Ext(p.ClientId), p.SiteCode, p.SiteName)).ToList(),
                Posts = chunk.Select(x => x.Place).DistinctBy(p => p.PostExternalId)
                    .Select(p => new PortalPostRow(p.PostExternalId, p.SiteExternalId, p.PostCode, p.PostName, p.Position)).ToList(),
                Guards = chunk.DistinctBy(x => x.Punch.EmployeeId)
                    .Select(x => PortalPlaces.GuardRow(x.Punch.EmployeeId!.Value, x.Place.ClientId, source.Employees.GetValueOrDefault(x.Punch.EmployeeId.Value))).ToList(),
                Punches = chunk.Select(x => new PortalLivePunchRow(
                    PortalPlaces.Ext(x.Punch.Id), PortalPlaces.Ext(x.Place.ClientId), x.Place.SiteExternalId, x.Place.PostExternalId,
                    PortalPlaces.Ext(x.Punch.EmployeeId!.Value), x.Punch.WorkDateTime, SourceName(x.Punch.LogSource), Clip(x.Punch.DeviceName, 100))).ToList(),
            });
        }
        return messages;
    }

    /// <summary>
    /// The manning of each client for the days given: every shift its posts need (from the requirements in force, with the start and end of
    /// the requirement or its time shift) and everyone deployed there. One message per client that has posts; nothing for the others.
    /// </summary>
    public static List<ClientPortalManningPublished> Manning(ManningSource source, Guid tenantId, DateOnly from, DateOnly to)
    {
        var messages = new List<ClientPortalManningPublished>();
        foreach (var clientSites in source.Sites.Where(s => source.Clients.ContainsKey(s.ClientId)).GroupBy(s => s.ClientId))
        {
            var siteIds = clientSites.Select(s => s.Id).ToHashSet();
            var posts = source.Posts.Values.Where(p => siteIds.Contains(p.ClientSiteId)).OrderBy(p => p.Code).ToList();
            if (posts.Count == 0)
                continue;
            var postIds = posts.Select(p => p.Id).ToHashSet();

            var shifts = new List<PortalShiftPlanRow>();
            foreach (var requirement in source.Requirements.Where(r => postIds.Contains(r.PostId)))
            {
                for (var day = from; day <= to; day = day.AddDays(1))
                {
                    if (!PostRequirementRules.AppliesOn(requirement, day))
                        continue;
                    var (start, end) = Times(requirement, day, source.ShiftTimes);
                    shifts.Add(new PortalShiftPlanRow(PortalPlaces.Ext(requirement.PostId), day, requirement.ShiftLabel.Trim(), start, end, requirement.RequiredHeadcount));
                }
            }

            var deployments = source.Deployments
                .Where(d => postIds.Contains(d.PostId) && d.StartDate <= to && (d.EndDate == null || d.EndDate >= from))
                .ToList();
            var clientId = clientSites.Key;
            messages.Add(new ClientPortalManningPublished
            {
                TenantId = tenantId,
                From = from,
                To = to,
                Clients = [PortalPlaces.ClientRow(source.Clients[clientId])],
                Sites = clientSites.OrderBy(s => s.Code).Select(PortalPlaces.SiteRow).ToList(),
                Posts = posts.Select(PortalPlaces.PostRow).ToList(),
                Guards = deployments.Select(d => d.EmployeeId).Distinct()
                    .Select(id => PortalPlaces.GuardRow(id, clientId, source.Employees.GetValueOrDefault(id))).ToList(),
                Shifts = shifts.DistinctBy(s => (s.PostExternalId, s.WorkDate, s.ShiftName.ToUpperInvariant())).ToList(),
                Assignments = deployments.Select(d => new PortalAssignmentRow(
                    PortalPlaces.Ext(d.PostId), PortalPlaces.Ext(d.EmployeeId), d.Role.ToString(), d.StartDate, d.EndDate, d.EventTag)).ToList(),
            });
        }
        return messages;
    }

    /// <summary>
    /// The shift's start and end on a day: the requirement's own times, else its time shift's; none when neither is known. A shift that
    /// ends at or before its start ends the next day.
    /// </summary>
    public static (DateTime? Start, DateTime? End) Times(PostRequirement r, DateOnly day, IReadOnlyDictionary<Guid, (TimeSpan Start, TimeSpan End)> shiftTimes)
    {
        TimeSpan? start = r.StartTime?.ToTimeSpan(), end = r.EndTime?.ToTimeSpan();
        if ((start is null || end is null) && r.TimeShiftId is { } id && shiftTimes.TryGetValue(id, out var shift))
        {
            start ??= shift.Start;
            end ??= shift.End;
        }
        if (start is not { } s || end is not { } e)
            return (null, null);
        var startAt = day.ToDateTime(TimeOnly.MinValue).Add(s);
        var endAt = day.ToDateTime(TimeOnly.MinValue).Add(e);
        return (startAt, endAt <= startAt ? endAt.AddDays(1) : endAt);
    }

    public static string SourceName(LOGSOURCE source) => source switch
    {
        LOGSOURCE.SYNC or LOGSOURCE.BIOMETRIC => "Device",
        LOGSOURCE.MOBILE or LOGSOURCE.GEOFENCE => "Mobile",
        LOGSOURCE.UPLOADED => "Upload",
        LOGSOURCE.MANUAL => "Manual",
        _ => "Other",
    };

    private static string? Clip(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
}
