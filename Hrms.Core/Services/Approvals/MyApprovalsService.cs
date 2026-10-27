using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Approvals;

/// <summary>One request on the signed-in person's approval lists (waiting on them, or still in progress).</summary>
public sealed record MyApprovalItem(
    ApprovalApplicationType ApplicationType,
    Guid ApplicationId,
    Guid ApplicantEmployeeId,
    string ApplicantName,
    string Summary,
    DateTime FiledAt,
    int StepNumber,
    int TotalSteps,
    string? FlowName,
    NoteRequirement NoteRequirement,
    Guid? OnBehalfOfEmployeeId,
    string? OnBehalfOfName,
    string? CurrentApproverLabel = null);

/// <summary>One finished request the signed-in person decided (as themself or through a delegate).</summary>
public sealed record MyApprovalHistoryItem(
    ApprovalApplicationType ApplicationType,
    Guid ApplicationId,
    Guid ApplicantEmployeeId,
    string ApplicantName,
    string Summary,
    DateTime FiledAt,
    ApprovalInstanceStatus FinalStatus,
    ApprovalActionType MyDecision,
    DateTime DecidedAt,
    string? Note,
    string? OnBehalfOfName,
    string? DecidedByName,
    IReadOnlyList<MyApprovalTrailEntry> Trail);

/// <summary>One step's decision in a finished request's trail: who approved or declined it, and when.</summary>
public sealed record MyApprovalTrailEntry(
    int StepNumber,
    ApprovalActionType Action,
    string? ApproverName,
    string? OnBehalfOfName,
    DateTime At,
    string? Note);

/// <summary>Who may act on a request: as themself, for a delegator, or not at all.</summary>
public sealed record CallerStanding(ApprovalStanding Standing, bool IsFallbackStep);

// "My Approvals": the in-progress requests of the employee request types, split into the ones waiting
// on the caller right now (pending) and the ones the caller follows that are on someone else's step
// (in progress). Evaluates every request in memory against preloaded people, so one list load is a
// handful of queries however many requests are pending.
public class MyApprovalsService
{
    private const string ApproverAudience = "Applicant";

    private readonly IUnitOfWorkService _uow;
    private readonly ApprovalEngineService _engine;
    private readonly IReadOnlyDictionary<ApprovalApplicationType, IApprovalRequestAdapter> _adapters;

    public MyApprovalsService(IUnitOfWorkService uow, ApprovalEngineService engine, IEnumerable<IApprovalRequestAdapter> adapters)
    {
        _uow = uow;
        _engine = engine;
        _adapters = adapters.ToDictionary(a => a.Type);
    }

    public IApprovalRequestAdapter? AdapterFor(ApprovalApplicationType type) => _adapters.GetValueOrDefault(type);

    // Everything one list load evaluates against.
    private sealed record Snapshot(
        Employee Caller,
        List<ApprovalInstance> Instances,
        Dictionary<Guid, Employee> Applicants,
        Dictionary<ApprovalApplicationType, IReadOnlyCollection<Employee>> DelegatorsByType);

    /// <param name="hasApprovePermission">Whether the caller holds <c>{prefix}:Approve</c> for a type's permission row.</param>
    public async Task<List<MyApprovalItem>> GetPendingAsync(
        Guid callerEmployeeId, Func<string, bool> hasApprovePermission, CancellationToken token)
    {
        var snapshot = await LoadAsync(callerEmployeeId, token);
        if (snapshot == null) return [];

        var mine = new List<(ApprovalInstance Instance, ApprovalStanding Standing)>();
        foreach (var instance in snapshot.Instances)
        {
            var standing = await WaitingOnCallerAsync(snapshot, instance, hasApprovePermission, token);
            if (standing != null) mine.Add((instance, standing));
        }
        return await BuildItemsAsync(snapshot, mine, token);
    }

    /// <summary>
    /// Requests still being approved that aren't the caller's turn but that they're part of: ones they
    /// already acted on this cycle, and ones a later step of the workflow will bring to them (or to
    /// someone who delegated to them). An Owner or Admin (<paramref name="seesEverything"/>) sees every
    /// request still in flight.
    /// </summary>
    public async Task<List<MyApprovalItem>> GetInProgressAsync(
        Guid callerEmployeeId, Func<string, bool> hasApprovePermission, CancellationToken token, bool seesEverything = false)
    {
        var snapshot = await LoadAsync(callerEmployeeId, token);
        if (snapshot == null) return [];

        var followed = new List<(ApprovalInstance Instance, ApprovalStanding Standing)>();
        foreach (var instance in snapshot.Instances)
        {
            if (await WaitingOnCallerAsync(snapshot, instance, hasApprovePermission, token) != null) continue;
            if (seesEverything || await FollowsAsync(snapshot, instance, token))
                followed.Add((instance, ApprovalStanding.None));
        }
        return await BuildItemsAsync(snapshot, followed, token);
    }

    /// <summary>
    /// Requests that are finished (approved, declined or cancelled) and that the caller decided -- as
    /// themself or through a delegate -- newest first. What <see cref="GetPendingAsync"/> and
    /// <see cref="GetInProgressAsync"/> drop once the request leaves InProgress.
    /// </summary>
    public async Task<List<MyApprovalHistoryItem>> GetHistoryAsync(
        Guid callerEmployeeId, DateOnly from, DateOnly to, int take, CancellationToken token)
    {
        // Decisions are stamped in UTC; the range is Manila days, so bound it by Manila midnights.
        var start = PhTime.StartOfDayUtc(from);
        var end = PhTime.StartOfDayUtc(to.AddDays(1));

        var types = _adapters.Keys.ToList();
        var instances = await _uow.Repository.Find<ApprovalInstance>(i =>
                i.Status != ApprovalInstanceStatus.InProgress && types.Contains(i.ApplicationType) &&
                i.Actions.Any(a => (a.Action == ApprovalActionType.Approved || a.Action == ApprovalActionType.Declined) &&
                                   (a.ActorEmployeeId == callerEmployeeId || a.OnBehalfOfEmployeeId == callerEmployeeId) &&
                                   a.CreatedAt >= start && a.CreatedAt < end))
            .Include(i => i.Actions)
            .AsNoTracking()
            .OrderByDescending(i => i.Actions.Max(a => a.CreatedAt))
            .Take(take)
            .ToListAsync(token);
        if (instances.Count == 0) return [];

        // The caller's latest decision in the range on each request (they may have decided in an earlier cycle too).
        var decided = instances.Select(i => (Instance: i, Action: i.Actions
                .Where(a => (a.Action == ApprovalActionType.Approved || a.Action == ApprovalActionType.Declined) &&
                            (a.ActorEmployeeId == callerEmployeeId || a.OnBehalfOfEmployeeId == callerEmployeeId) &&
                            a.CreatedAt >= start && a.CreatedAt < end)
                .OrderByDescending(a => a.CreatedAt).First()))
            .ToList();

        var summaries = new Dictionary<(ApprovalApplicationType, Guid), ApprovalRequestSummary>();
        foreach (var group in decided.GroupBy(d => d.Instance.ApplicationType))
        {
            var described = await _adapters[group.Key].DescribeAsync(group.Select(d => d.Instance.ApplicationId).ToList(), token);
            foreach (var (id, summary) in described) summaries[(group.Key, id)] = summary;
        }

        // Every step's decision of the request's latest cycle, so the trail can say who approved each step.
        static bool IsTrailAction(ApprovalInstance i, ApprovalAction a) =>
            a.CycleNumber == i.CycleNumber &&
            a.Action is ApprovalActionType.Approved or ApprovalActionType.Declined or ApprovalActionType.Skipped;

        var nameIds = decided.Select(d => d.Instance.ApplicantEmployeeId)
            .Concat(decided.Select(d => d.Action.OnBehalfOfEmployeeId).OfType<Guid>())
            .Concat(decided.Select(d => d.Action.ActorEmployeeId).OfType<Guid>())
            .Concat(decided.SelectMany(d => d.Instance.Actions.Where(a => IsTrailAction(d.Instance, a)))
                .SelectMany(a => new[] { a.ActorEmployeeId, a.OnBehalfOfEmployeeId }).OfType<Guid>())
            .Distinct().ToList();
        var names = (await _uow.Repository.Find<Employee>(e => nameIds.Contains(e.Id)).AsNoTracking().ToListAsync(token))
            .ToDictionary(e => e.Id, e => $"{e.FirstName} {e.LastName}".Trim());

        var items = new List<MyApprovalHistoryItem>();
        foreach (var (instance, action) in decided)
        {
            // Left out when the caller isn't allowed to see the record itself (confidential applicant).
            if (!summaries.TryGetValue((instance.ApplicationType, instance.ApplicationId), out var summary)) continue;
            var actedForOther = action.ActorEmployeeId == callerEmployeeId && action.OnBehalfOfEmployeeId is not null;
            var decidedByDelegate = action.OnBehalfOfEmployeeId == callerEmployeeId && action.ActorEmployeeId != callerEmployeeId;
            items.Add(new MyApprovalHistoryItem(
                instance.ApplicationType, instance.ApplicationId, instance.ApplicantEmployeeId,
                names.GetValueOrDefault(instance.ApplicantEmployeeId) ?? string.Empty,
                summary.Summary, summary.FiledAt, instance.Status, action.Action, action.CreatedAt, action.Note,
                actedForOther ? names.GetValueOrDefault(action.OnBehalfOfEmployeeId!.Value) : null,
                decidedByDelegate && action.ActorEmployeeId is { } actor ? names.GetValueOrDefault(actor) : null,
                instance.Actions.Where(a => IsTrailAction(instance, a))
                    .OrderBy(a => a.StepNumber).ThenBy(a => a.CreatedAt)
                    .Select(a => new MyApprovalTrailEntry(
                        a.StepNumber, a.Action,
                        a.ActorEmployeeId is { } who ? names.GetValueOrDefault(who) : null,
                        a.OnBehalfOfEmployeeId is { } onBehalf && onBehalf != a.ActorEmployeeId ? names.GetValueOrDefault(onBehalf) : null,
                        a.CreatedAt, a.Note))
                    .ToList()));
        }
        return items.OrderByDescending(i => i.DecidedAt).ToList();
    }

    private async Task<Snapshot?> LoadAsync(Guid callerEmployeeId, CancellationToken token)
    {
        var types = _adapters.Keys.ToList();
        var caller = await _uow.Repository.Find<Employee>(e => e.Id == callerEmployeeId).AsNoTracking().FirstOrDefaultAsync(token);
        if (caller == null) return null;

        // The approver/department/position navigations feed the "pending with ..." label.
        var instances = await _uow.Repository.Find<ApprovalInstance>(i =>
                i.Status == ApprovalInstanceStatus.InProgress && types.Contains(i.ApplicationType))
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.NamedApprovers)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.Groups).ThenInclude(g => g.ApproverDepartment)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.Groups).ThenInclude(g => g.ApproverPosition)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.ApproverDepartment)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.ApproverPosition)
            .Include(i => i.Actions)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(token);
        if (instances.Count == 0) return null;

        var applicantIds = instances.Select(i => i.ApplicantEmployeeId).Distinct().ToList();
        var applicants = (await _uow.Repository.Find<Employee>(e => applicantIds.Contains(e.Id)).AsNoTracking().ToListAsync(token))
            .ToDictionary(e => e.Id);

        var delegatorsByType = new Dictionary<ApprovalApplicationType, IReadOnlyCollection<Employee>>();
        foreach (var type in instances.Select(i => i.ApplicationType).Distinct())
            delegatorsByType[type] = await _engine.LoadDelegatorsAsync(callerEmployeeId, type, token);

        return new Snapshot(caller, instances, applicants, delegatorsByType);
    }

    // The caller's standing when the request is waiting on them right now; null when it isn't.
    private async Task<ApprovalStanding?> WaitingOnCallerAsync(
        Snapshot snapshot, ApprovalInstance instance, Func<string, bool> hasApprovePermission, CancellationToken token)
    {
        if (!snapshot.Applicants.TryGetValue(instance.ApplicantEmployeeId, out var applicant)) return null;
        var standing = await _engine.GetStandingAsync(
            instance, snapshot.Caller, applicant, snapshot.DelegatorsByType[instance.ApplicationType], token);
        if (!standing.Eligible) return null;
        // Acting as themself still needs the type's Approve permission; acting for a delegator, or as the
        // person a late step was escalated to, doesn't.
        if (!standing.GrantsAccess && !hasApprovePermission(_adapters[instance.ApplicationType].PermissionPrefix)) return null;
        // Already decided this step (as themself or through their delegate).
        var key = standing.OnBehalfOfEmployeeId ?? snapshot.Caller.Id;
        if (instance.Actions.Any(a => a.StepNumber == instance.CurrentStepNumber && a.CycleNumber == instance.CycleNumber &&
                                      (a.OnBehalfOfEmployeeId ?? a.ActorEmployeeId) == key && a.Action != ApprovalActionType.Reassigned))
            return null;
        return standing;
    }

    // Whether a request that isn't the caller's turn still belongs on their In Progress list.
    private async Task<bool> FollowsAsync(Snapshot snapshot, ApprovalInstance instance, CancellationToken token)
    {
        if (!snapshot.Applicants.TryGetValue(instance.ApplicantEmployeeId, out var applicant)) return false;
        // Only requests the caller is part of: holding the type's Approve permission is not enough on
        // its own (Owner/Admin and HR see the whole record in the module's own Applications list).
        var delegators = snapshot.DelegatorsByType[instance.ApplicationType];
        var actedFor = delegators.Select(d => d.Id).Append(snapshot.Caller.Id).ToHashSet();
        if (instance.Actions.Any(a => a.CycleNumber == instance.CycleNumber && a.Action != ApprovalActionType.Reassigned &&
                                      ((a.ActorEmployeeId is { } actor && actor == snapshot.Caller.Id) ||
                                       (a.OnBehalfOfEmployeeId is { } onBehalf && actedFor.Contains(onBehalf)))))
            return true;

        // Coming to them later -- and they'll be able to act when it does (same rule as pending).
        var later = await _engine.GetLaterStepStandingAsync(instance, snapshot.Caller, applicant, delegators, token);
        return later.Eligible && later.GrantsAccess;
    }

    private async Task<List<MyApprovalItem>> BuildItemsAsync(
        Snapshot snapshot, List<(ApprovalInstance Instance, ApprovalStanding Standing)> rows, CancellationToken token)
    {
        if (rows.Count == 0) return [];

        var summaries = new Dictionary<(ApprovalApplicationType, Guid), ApprovalRequestSummary>();
        foreach (var group in rows.GroupBy(m => m.Instance.ApplicationType))
        {
            var described = await _adapters[group.Key].DescribeAsync(group.Select(m => m.Instance.ApplicationId).ToList(), token);
            foreach (var (id, summary) in described) summaries[(group.Key, id)] = summary;
        }

        // Who each current step waits on, worked out per applicant for the dynamic approver types.
        var resolvedByInstance = new Dictionary<Guid, Dictionary<(int Step, Guid? Group), ResolvedApprovers>>();
        foreach (var (instance, _) in rows)
        {
            var resolvedByPool = new Dictionary<(int Step, Guid? Group), ResolvedApprovers>();
            if (CurrentStep(instance) is { } step && snapshot.Applicants.TryGetValue(instance.ApplicantEmployeeId, out var applicant))
            {
                foreach (var pool in ApprovalStepPools.Of(step))
                {
                    if (await _engine.ResolvePoolApproversAsync(pool.Effective, applicant, token) is { } resolved)
                        resolvedByPool[(step.StepNumber, pool.GroupId)] = resolved;
                }
            }
            resolvedByInstance[instance.Id] = resolvedByPool;
        }

        // Delegators acted for, the current steps' named people, reassigned approvers and resolved ones, in one lookup.
        var nameIds = rows.Select(m => m.Standing.OnBehalfOfEmployeeId).OfType<Guid>()
            .Concat(rows.Select(m => CurrentStep(m.Instance)).OfType<ApprovalWorkflowStep>().SelectMany(s =>
                ApprovalStepPools.Of(s).Select(p => p.Effective.ApproverEmployeeId).OfType<Guid>()))
            .Concat(rows.Select(m => m.Instance.ReassignedApproverEmployeeId).OfType<Guid>())
            .Concat(resolvedByInstance.Values.SelectMany(r => r.Values).SelectMany(r => r.EmployeeIds))
            .Distinct()
            .ToList();
        var names = nameIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _uow.Repository.Find<Employee>(e => nameIds.Contains(e.Id)).AsNoTracking().ToListAsync(token))
                .ToDictionary(e => e.Id, e => $"{e.FirstName} {e.LastName}".Trim());

        var items = new List<MyApprovalItem>();
        foreach (var (instance, standing) in rows)
        {
            // Left out when the caller isn't allowed to see the record itself (confidential applicant).
            if (!summaries.TryGetValue((instance.ApplicationType, instance.ApplicationId), out var summary)) continue;
            var applicant = snapshot.Applicants[instance.ApplicantEmployeeId];
            var step = CurrentStep(instance);
            var approverLabel = instance.ReassignedApproverEmployeeId is { } reassignedId
                ? names.GetValueOrDefault(reassignedId)
                : ApprovalStepLabeler.StepLabel(step, names, resolvedByInstance[instance.Id], ApproverAudience);
            items.Add(new MyApprovalItem(
                instance.ApplicationType, instance.ApplicationId, applicant.Id,
                $"{applicant.FirstName} {applicant.LastName}".Trim(),
                summary.Summary, summary.FiledAt,
                instance.CurrentStepNumber, instance.Workflow?.Steps.Count ?? 1,
                instance.Workflow?.Name,
                step?.NoteRequirement ?? NoteRequirement.None,
                standing.OnBehalfOfEmployeeId,
                standing.OnBehalfOfEmployeeId is { } d ? names.GetValueOrDefault(d) : null,
                string.IsNullOrWhiteSpace(approverLabel) ? null : approverLabel));
        }
        return items.OrderBy(i => i.FiledAt).ToList();
    }

    private static ApprovalWorkflowStep? CurrentStep(ApprovalInstance instance) =>
        instance.Workflow?.Steps.SingleOrDefault(s => s.StepNumber == instance.CurrentStepNumber);

    /// <summary>The caller's standing on one request, for the approve/decline endpoints' access check.</summary>
    public async Task<CallerStanding> GetStandingAsync(
        ApprovalApplicationType type, Guid applicationId, Guid callerEmployeeId, CancellationToken token)
    {
        var instance = await _engine.GetInstanceReadOnlyAsync(type, applicationId, token);
        if (instance is null || instance.Status != ApprovalInstanceStatus.InProgress)
            return new CallerStanding(ApprovalStanding.None, false);

        var caller = await _uow.Repository.Find<Employee>(e => e.Id == callerEmployeeId).AsNoTracking().FirstOrDefaultAsync(token);
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == instance.ApplicantEmployeeId).AsNoTracking().FirstOrDefaultAsync(token);
        if (caller == null || applicant == null) return new CallerStanding(ApprovalStanding.None, false);

        var delegators = await _engine.LoadDelegatorsAsync(callerEmployeeId, type, token);
        var standing = await _engine.GetStandingAsync(instance, caller, applicant, delegators, token);
        var isFallback = instance.Workflow?.Steps.All(s => s.StepNumber != instance.CurrentStepNumber) ?? true;
        return new CallerStanding(standing, isFallback);
    }
}
