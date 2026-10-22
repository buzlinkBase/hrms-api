using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Analytics;

/// <summary>One filed request and its approval history, as the request figures need them.</summary>
public sealed record RequestFact(
    ApprovalApplicationType Type,
    ApprovalInstanceStatus Status,
    DateTime FiledAt,
    IReadOnlyList<RequestActionFact> Actions);

public sealed record RequestActionFact(Guid ActorEmployeeId, ApprovalActionType Action, DateTime At);

public sealed record LeaveDaysFact(string LeaveType, decimal Days, Guid? LeaveId = null);

/// <summary>
/// Leave and request analytics: how many employee requests were filed, how many got approved,
/// how long approval takes, what's still waiting, who approves and how fast, and leave days
/// taken by type. Covers the requests employees file (leave, overtime, official business, pass
/// slip, loans, profile updates) -- DTR and payroll batches have their own screens.
/// </summary>
public class RequestAnalyticsService
{
    public static readonly IReadOnlySet<ApprovalApplicationType> EmployeeRequestTypes = new HashSet<ApprovalApplicationType>
    {
        ApprovalApplicationType.Leave,
        ApprovalApplicationType.Overtime,
        ApprovalApplicationType.OfficialBusiness,
        ApprovalApplicationType.PassSlip,
        ApprovalApplicationType.Loan,
        ApprovalApplicationType.ProfileUpdate,
    };

    private readonly IUnitOfWorkService _uow;

    public RequestAnalyticsService(IUnitOfWorkService uow) => _uow = uow;

    private async Task<List<RequestFact>> RequestsAsync(AnalyticsFilter filter, CancellationToken token)
    {
        var employeeIds = AnalyticsScope.EmployeeIds(_uow.Repository, filter);
        var from = filter.From.ToDateTime(TimeOnly.MinValue);
        var until = filter.To.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var types = EmployeeRequestTypes.ToList();

        var rows = await _uow.Repository.FindAll<ApprovalInstance>().AsNoTracking()
            .Where(x => types.Contains(x.ApplicationType) && x.CreatedAt >= from && x.CreatedAt < until &&
                        employeeIds.Contains(x.ApplicantEmployeeId))
            .Select(x => new
            {
                x.ApplicationType,
                x.Status,
                x.CreatedAt,
                Actions = x.Actions.Select(a => new { a.ActorEmployeeId, a.Action, a.CreatedAt }).ToList(),
            })
            .ToListAsync(token);

        return rows.Select(r => new RequestFact(
            r.ApplicationType, r.Status, r.CreatedAt,
            r.Actions.Select(a => new RequestActionFact(a.ActorEmployeeId, a.Action, a.CreatedAt)).OrderBy(a => a.At).ToList())).ToList();
    }

    public async Task<AnalyticsResult> GetAsync(AnalyticsFilter filter, CancellationToken token)
    {
        filter = AnalyticsPeriod.Normalize(filter);
        var current = await RequestsAsync(filter, token);
        var previous = await RequestsAsync(AnalyticsPeriod.Previous(filter), token);

        var employeeIds = AnalyticsScope.EmployeeIds(_uow.Repository, filter);
        var leaves = await _uow.Repository.FindAll<LeaveApplication>().AsNoTracking()
            .Where(x => x.ApprovalStatus == ApprovalStatus.Approved &&
                        x.LeaveDateFrom >= filter.From && x.LeaveDateFrom <= filter.To &&
                        employeeIds.Contains(x.EmployeeId))
            .Select(x => new { LeaveType = x.Leave.Description, x.LeaveId, x.LeaveDateFrom, x.LeaveDateTo, x.DayFraction })
            .ToListAsync(token);

        var actorIds = current.SelectMany(r => r.Actions).Select(a => a.ActorEmployeeId).Distinct().ToList();
        var actorNames = await _uow.Repository.FindAll<Employee>().AsNoTracking()
            .Where(e => actorIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => (e.FirstName + " " + e.LastName).Trim(), token);

        return Calculate(
            filter,
            current,
            previous,
            leaves.Select(l => new LeaveDaysFact(
                string.IsNullOrWhiteSpace(l.LeaveType) ? "Other" : l.LeaveType,
                LeaveDays(l.LeaveDateFrom, l.LeaveDateTo, l.DayFraction),
                l.LeaveId)).ToList(),
            id => actorNames.GetValueOrDefault(id) ?? "—");
    }

    /// <summary>Calendar days covered, halved for an AM/PM half-day.</summary>
    public static decimal LeaveDays(DateOnly from, DateOnly to, DayFraction fraction)
    {
        var days = Math.Max(1, to.DayNumber - from.DayNumber + 1);
        return fraction == DayFraction.FullDay ? days : days * 0.5m;
    }

    private static bool IsResolved(RequestFact r) =>
        r.Status is ApprovalInstanceStatus.Approved or ApprovalInstanceStatus.Declined;

    /// <summary>Hours from filing to the final approve/decline click.</summary>
    private static double? HoursToResolve(RequestFact r)
    {
        if (!IsResolved(r)) return null;
        var decided = r.Actions.LastOrDefault(a => a.Action is ApprovalActionType.Approved or ApprovalActionType.Declined);
        return decided == null ? null : (decided.At - r.FiledAt).TotalHours;
    }

    public static decimal Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return 0;
        var mid = sorted.Count / 2;
        var median = sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
        return Math.Round((decimal)median, 1);
    }

    private sealed record Totals(decimal Filed, decimal ApprovalRate, decimal MedianHours, decimal Pending);

    private static Totals TotalsOf(IReadOnlyList<RequestFact> requests)
    {
        var approved = requests.Count(r => r.Status == ApprovalInstanceStatus.Approved);
        var declined = requests.Count(r => r.Status == ApprovalInstanceStatus.Declined);
        return new Totals(
            requests.Count,
            AnalyticsPeriod.Percent(approved, approved + declined),
            Median(requests.Select(HoursToResolve).OfType<double>()),
            requests.Count(r => r.Status == ApprovalInstanceStatus.InProgress));
    }

    public static string TypeLabel(ApprovalApplicationType type) => type switch
    {
        ApprovalApplicationType.OfficialBusiness => "Official Business",
        ApprovalApplicationType.PassSlip => "Pass Slip",
        ApprovalApplicationType.Loan => "Loan",
        ApprovalApplicationType.ProfileUpdate => "Profile Update",
        _ => type.ToString(),
    };

    /// <summary>camelCase, as the API writes dictionary keys -- so a point's keys match its series definitions.</summary>
    public static string SeriesKey(ApprovalApplicationType type) =>
        char.ToLowerInvariant(type.ToString()[0]) + type.ToString()[1..];

    public static AnalyticsResult Calculate(
        AnalyticsFilter filter,
        IReadOnlyList<RequestFact> current,
        IReadOnlyList<RequestFact> previous,
        IReadOnlyList<LeaveDaysFact> leaveDays,
        Func<Guid, string> approverName)
    {
        var now = TotalsOf(current);
        var before = TotalsOf(previous);
        var kpis = new List<AnalyticsKpi>
        {
            new("filed", "Requests filed", now.Filed, before.Filed, AnalyticsUnit.Count),
            new("approvalRate", "Approval rate", now.ApprovalRate, before.Filed == 0 ? null : before.ApprovalRate, AnalyticsUnit.Percent),
            new("medianHours", "Median time to decide", now.MedianHours, before.Filed == 0 ? null : before.MedianHours, AnalyticsUnit.Hours),
            new("pending", "Still waiting", now.Pending, before.Pending, AnalyticsUnit.Count),
            new("leaveDays", "Leave days taken", leaveDays.Sum(l => l.Days), null, AnalyticsUnit.Days),
        };

        var types = EmployeeRequestTypes.OrderBy(TypeLabel).ToList();
        var series = AnalyticsPeriod.Months(filter.From, filter.To)
            .Select(month => new AnalyticsPoint(month.Label, types.ToDictionary(
                SeriesKey,
                t => (decimal)current.Count(r => r.Type == t &&
                    DateOnly.FromDateTime(r.FiledAt) >= month.Start && DateOnly.FromDateTime(r.FiledAt) <= month.End))))
            .ToList();

        var byType = types
            .Select(t => (Type: t, Requests: current.Where(r => r.Type == t).ToList()))
            .Where(x => x.Requests.Count > 0)
            .Select(x =>
            {
                var totals = TotalsOf(x.Requests);
                // Keyed like the series, so the page drills into a row and a chart point the same way.
                return new AnalyticsRow(TypeLabel(x.Type), new Dictionary<string, decimal>
                {
                    ["filed"] = totals.Filed,
                    ["approved"] = x.Requests.Count(r => r.Status == ApprovalInstanceStatus.Approved),
                    ["declined"] = x.Requests.Count(r => r.Status == ApprovalInstanceStatus.Declined),
                    ["pending"] = totals.Pending,
                    ["medianHours"] = totals.MedianHours,
                }, SeriesKey(x.Type));
            })
            .OrderByDescending(r => r.Values["filed"])
            .ToList();

        // Each approver's response time: from when the request reached them -- filing for the
        // first step, the previous click for a later one -- to their own click.
        var responses = current.SelectMany(r => r.Actions
            .Where(a => a.Action is ApprovalActionType.Approved or ApprovalActionType.Declined)
            .Select((a, i) => (a.ActorEmployeeId, a.Action, Hours: (a.At - (i == 0 ? r.FiledAt : r.Actions
                .Where(p => p.Action is ApprovalActionType.Approved or ApprovalActionType.Declined)
                .ElementAt(i - 1).At)).TotalHours)));
        var byApprover = responses
            .GroupBy(x => x.ActorEmployeeId)
            .Select(g => new AnalyticsRow(approverName(g.Key), new Dictionary<string, decimal>
            {
                ["decisions"] = g.Count(),
                ["approved"] = g.Count(x => x.Action == ApprovalActionType.Approved),
                ["declined"] = g.Count(x => x.Action == ApprovalActionType.Declined),
                ["medianHours"] = Median(g.Select(x => x.Hours)),
            }))
            .OrderByDescending(r => r.Values["decisions"])
            .ToList();

        var byLeaveType = leaveDays
            .GroupBy(l => (l.LeaveType, l.LeaveId))
            .Select(g => new AnalyticsRow(g.Key.LeaveType, new Dictionary<string, decimal>
            {
                ["days"] = g.Sum(l => l.Days),
                ["requests"] = g.Count(),
            }, g.Key.LeaveId?.ToString()))
            .OrderByDescending(r => r.Values["days"])
            .ToList();

        return new AnalyticsResult(
            filter.From,
            filter.To,
            kpis,
            types.Select(t => new AnalyticsSeries(SeriesKey(t), TypeLabel(t), AnalyticsUnit.Count)).ToList(),
            series,
            [
                new AnalyticsBreakdown("type", "By request type",
                [
                    new("filed", "Filed", AnalyticsUnit.Count),
                    new("approved", "Approved", AnalyticsUnit.Count),
                    new("declined", "Declined", AnalyticsUnit.Count),
                    new("pending", "Still waiting", AnalyticsUnit.Count),
                    new("medianHours", "Median hours to decide", AnalyticsUnit.Hours),
                ], byType),
                new AnalyticsBreakdown("approver", "By approver",
                [
                    new("decisions", "Decisions", AnalyticsUnit.Count),
                    new("approved", "Approved", AnalyticsUnit.Count),
                    new("declined", "Declined", AnalyticsUnit.Count),
                    new("medianHours", "Median response hours", AnalyticsUnit.Hours),
                ], byApprover),
                new AnalyticsBreakdown("leaveType", "Leave days by type",
                [
                    new("days", "Days", AnalyticsUnit.Days),
                    new("requests", "Approved requests", AnalyticsUnit.Count),
                ], byLeaveType),
            ]);
    }
}
