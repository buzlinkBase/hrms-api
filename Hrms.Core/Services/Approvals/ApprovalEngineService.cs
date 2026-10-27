using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Core.Messaging;
using MassTransit;
using Onepunch.AuditEngine;

namespace Hrms.Core.Services.Approvals;

public record ApprovalActionResult(ApprovalInstanceStatus InstanceStatus, int CurrentStepNumber);

// The runtime approval engine, shared across every Applications type (Leave/Overtime/Official
// Business/Pass Slip/Loan/Profile Update/Payroll Posting). Owns ApprovalInstance/
// ApprovalAction state only -- it never touches LeaveApplication.ApprovalStatus or its
// siblings directly (it has no generic way to, since they're unrelated tables). Callers (each
// application's own service) call StartAsync right after creating their record, and
// RecordActionAsync from their approve/decline action, then apply the returned
// ApprovalActionResult.InstanceStatus onto their own entity's legacy ApprovalStatus field.
//
// Reads/writes go entirely through _uow.Repository's generic methods (Find<T>/AddAsync<T>/
// Update<T>) rather than raw Context access, matching how every other service in this project
// stays unit-testable against the standard IRepository/IUnitOfWorkService NSubstitute mocks --
// see ApprovalEligibilityTests and MeControllerTests' portal-application tests.
//
// Neither method commits -- callers own the transaction boundary so the instance/action write
// and the application's own ApprovalStatus write land in one CommitChangesAsync. Publishing
// notification events (see PublishStepNotificationAsync/PublishResolutionNotificationAsync)
// happens before that commit too, on purpose -- the EF outbox (AddEntityFrameworkOutbox +
// UseBusOutbox in RabbitMqConfiguration) only actually delivers a Publish call made within the
// same DbContext transaction as the eventual SaveChanges, so it has to happen here, not after.
public class ApprovalEngineService
{
    private readonly IUnitOfWorkService _uow;
    private readonly IPublishEndpoint _publisher;
    private readonly IAuditWriter? _audit;
    private readonly IApprovalDelegationLookup? _delegations;
    private readonly ApprovalApproverResolver _approvers;
    private readonly ApproverEligibilityResolver _eligibility = new();

    // auditWriter and delegations are optional so the many direct constructions in tests stay
    // valid; the container always supplies them (AddHrmsAuditTrail / ApprovalDelegationService).
    public ApprovalEngineService(
        IUnitOfWorkService uow, IPublishEndpoint publisher, IAuditWriter? auditWriter = null,
        IApprovalDelegationLookup? delegations = null, ApprovalApproverResolver? approverResolver = null)
    {
        _uow = uow;
        _publisher = publisher;
        _audit = auditWriter;
        _delegations = delegations;
        _approvers = approverResolver ?? new ApprovalApproverResolver(new RepositoryOrgLookup(uow));
    }

    public async Task<ApprovalInstance> StartAsync(
        ApprovalApplicationType applicationType,
        Guid applicationId,
        Guid applicantEmployeeId,
        CancellationToken token = default,
        Guid? actorEmployeeId = null,
        ApprovalScopeContext? scope = null,
        ApprovalRequestAttributes? attributes = null)
    {
        // Whoever filed it already knows -- they're never told "Pending Your Approval" for their own filing.
        var filedBy = actorEmployeeId ?? applicantEmployeeId;
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == applicantEmployeeId)
            .AsNoTracking()
            .FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Applicant not found.");

        // Batch approvals (DTR, payroll) pass their own scope; everything else is scoped by the applicant.
        // The request's own measures (days, hours, amount, leave type) route it too: a workflow can be
        // scoped to a leave type, and individual steps can apply only above a threshold.
        var workflow = await ResolveActiveWorkflowAsync(applicationType, scope ?? ApprovalScopeContext.FromEmployee(applicant, attributes), token);

        // IX_ApprovalInstances_ApplicationType_ApplicationId is unique -- "one live instance per
        // application record" (see ApprovalInstanceConfig). A prior cycle for this exact
        // application can already exist and be resolved (e.g. a declined DTR/Payroll deletion
        // request being re-requested) without ever being cleaned up, so blindly inserting a new
        // row here would violate that constraint. Reuse and reset that same row for the new
        // cycle instead -- its past Actions are left in place as audit history ("declined, then
        // re-requested"), only the cycle state resets. An instance still InProgress is returned
        // as-is (idempotent) rather than restarted out from under whoever's already reviewing it.
        var existing = await GetInstanceAsync(applicationType, applicationId, token);
        if (existing != null)
        {
            if (existing.Status == ApprovalInstanceStatus.InProgress) return existing;

            existing.ApplicantEmployeeId = applicantEmployeeId;
            existing.ApprovalWorkflowId = workflow?.Id;
            existing.Workflow = workflow;
            existing.CurrentStepNumber = 1;
            existing.Status = ApprovalInstanceStatus.InProgress;
            existing.CycleNumber += 1;
            ClearStepAssignments(existing);
            EnterStep(existing);
            existing.AttributesJson = attributes?.ToJson();
            _uow.Repository.Update(existing);

            var restartStep = workflow?.Steps.SingleOrDefault(s => s.StepNumber == 1);
            if (restartStep != null)
                await PublishStepNotificationAsync(existing, restartStep, applicant, filedBy, token);
            else
                await PublishPoolNotificationAsync(existing, applicant, token);

            return existing;
        }

        var instance = new ApprovalInstance
        {
            ApplicationType = applicationType,
            ApplicationId = applicationId,
            ApplicantEmployeeId = applicantEmployeeId,
            ApprovalWorkflowId = workflow?.Id,
            // Already-loaded (with Steps/NamedApprovers included by ResolveActiveWorkflowAsync)
            // so a caller that immediately reuses this returned instance -- see RecordActionAsync's
            // self-heal path -- doesn't need a second round-trip to see the same data a fresh
            // GetInstanceAsync query would have loaded.
            Workflow = workflow,
            CurrentStepNumber = 1,
            Status = ApprovalInstanceStatus.InProgress,
            StepEnteredAt = DateTime.UtcNow,
            AttributesJson = attributes?.ToJson(),
        };
        await _uow.Repository.AddAsync(instance, token);

        var firstStep = workflow?.Steps.SingleOrDefault(s => s.StepNumber == 1);
        if (firstStep != null)
            await PublishStepNotificationAsync(instance, firstStep, applicant, filedBy, token);
        else
            await PublishPoolNotificationAsync(instance, applicant, token);

        return instance;
    }

    /// <summary>
    /// The application was withdrawn while pending: its approval request stops, so nobody is
    /// asked to act on it any more and its workflow is free to change. Doesn't commit -- the
    /// caller saves it together with the application's own Withdrawn status.
    /// </summary>
    public async Task CancelAsync(ApprovalApplicationType applicationType, Guid applicationId, CancellationToken token = default)
    {
        var instance = await GetInstanceAsync(applicationType, applicationId, token);
        if (instance is null || instance.Status != ApprovalInstanceStatus.InProgress) return;

        instance.Status = ApprovalInstanceStatus.Cancelled;
        ClearStepAssignments(instance);
        _uow.Repository.Update(instance);
        await OnInstanceClosedAsync(instance, token);
    }

    // A request just stopped running on its workflow: if a new version is scheduled to take over
    // once this one drains, and this was the last one, switch now -- in the caller's same save.
    // Best-effort: the Approval Workflows list re-checks (ApprovalWorkflowService.ReconcileAsync),
    // so a hiccup here only delays the switch, never blocks the approval itself.
    private async Task OnInstanceClosedAsync(ApprovalInstance instance, CancellationToken token)
    {
        if (instance.ApprovalWorkflowId is not { } workflowId) return;
        var context = _uow.Context;
        if (context == null) return; // a mocked unit of work in unit tests
        try
        {
            await ApprovalWorkflowSwitch.TryActivateReplacementAsync(context, workflowId, instance.Id, token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "Couldn't check for a scheduled workflow switch after request {InstanceId} closed", instance.Id);
        }
    }

    // An application edited while still pending must be re-reviewed from the top if anyone has
    // already approved it -- otherwise an approver's sign-off would silently carry over to content
    // they never saw. Starts a new cycle at step 1 (prior cycles' actions stay as audit history,
    // same as StartAsync's restart branch) and re-notifies the first approvers. A pending
    // application nobody has approved yet is left alone: its first approvers were already
    // notified and nothing they signed off on changed. Doesn't commit -- callers own the
    // transaction, as with StartAsync.
    //
    // attributes: the edited request's new measures (days, hours, amount, leave type). They replace
    // the instance's snapshot, and the workflow is chosen again -- an edit that moves a request into
    // another workflow (a different leave type, say) starts it afresh on that one, even if nobody
    // had approved yet. Steps that depend on a measure are decided when the request reaches them, so
    // a later edit of the days or amount is picked up without a restart.
    public async Task<bool> RestartIfProgressedAsync(
        ApprovalApplicationType type, Guid applicationId, CancellationToken token = default,
        ApprovalRequestAttributes? attributes = null)
    {
        var instance = await GetInstanceAsync(type, applicationId, token);
        if (instance is null || instance.Status != ApprovalInstanceStatus.InProgress) return false;

        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == instance.ApplicantEmployeeId)
            .AsNoTracking()
            .FirstOrDefaultAsync(token);

        var workflowChanged = false;
        if (attributes != null)
        {
            instance.AttributesJson = attributes.ToJson();
            if (applicant != null)
            {
                var resolved = await ResolveActiveWorkflowAsync(type, ApprovalScopeContext.FromEmployee(applicant, attributes), token);
                workflowChanged = resolved?.Id != instance.ApprovalWorkflowId;
                if (workflowChanged)
                {
                    instance.ApprovalWorkflowId = resolved?.Id;
                    instance.Workflow = resolved;
                }
            }
        }

        var progressed = instance.CurrentStepNumber > 1 || instance.Actions.Any(a =>
            a.CycleNumber == instance.CycleNumber && a.Action == ApprovalActionType.Approved);
        if (!progressed && !workflowChanged)
        {
            if (attributes != null) _uow.Repository.Update(instance);
            return false;
        }

        instance.CycleNumber += 1;
        instance.CurrentStepNumber = 1;
        ClearStepAssignments(instance);
        EnterStep(instance);
        _uow.Repository.Update(instance);

        if (applicant is null) return true;

        var firstStep = instance.Workflow?.Steps.SingleOrDefault(s => s.StepNumber == 1);
        if (firstStep != null)
            await PublishStepNotificationAsync(instance, firstStep, applicant, applicant.Id, token);
        else
            await PublishPoolNotificationAsync(instance, applicant, token);

        return true;
    }

    // Several workflows can be active for a type at once, each limited to some scope (branch,
    // department, site...). The one the application runs on is the best match among them -- see
    // ApprovalScopeMatching. None matching = null, the implicit single-step fallback (see
    // ApprovalInstance.ApprovalWorkflowId). The few active rows are loaded and matched in memory.
    private async Task<ApprovalWorkflow?> ResolveActiveWorkflowAsync(
        ApprovalApplicationType type, ApprovalScopeContext scope, CancellationToken token)
    {
        var active = await _uow.Repository.Find<ApprovalWorkflow>(w => w.ApplicationType == type && w.IsActive)
            .Include(w => w.Scopes)
            .Include(w => w.Steps).ThenInclude(s => s.NamedApprovers)
            .Include(w => w.Steps).ThenInclude(s => s.Groups)
            .ToListAsync(token);
        return ApprovalScopeMatching.Select(active, scope);
    }

    // Tracked: the write paths (Start/Restart/RecordAction/Reassign) modify what this returns.
    public async Task<ApprovalInstance?> GetInstanceAsync(
        ApprovalApplicationType type, Guid applicationId, CancellationToken token = default) =>
        await InstanceQuery(type, applicationId).FirstOrDefaultAsync(token);

    // Untracked twin for callers that only display or evaluate the instance.
    public async Task<ApprovalInstance?> GetInstanceReadOnlyAsync(
        ApprovalApplicationType type, Guid applicationId, CancellationToken token = default) =>
        await InstanceQuery(type, applicationId).AsNoTracking().FirstOrDefaultAsync(token);

    // Steps -> NamedApprovers and Actions are sibling collections: as one SQL statement they
    // multiply into Steps x NamedApprovers x Actions rows, so the query is split per collection.
    private IQueryable<ApprovalInstance> InstanceQuery(ApprovalApplicationType type, Guid applicationId) =>
        _uow.Repository.Find<ApprovalInstance>(i => i.ApplicationType == type && i.ApplicationId == applicationId)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.NamedApprovers)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.Groups).ThenInclude(g => g.ApproverDepartment)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.Groups).ThenInclude(g => g.ApproverPosition)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.ApproverEmployee)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.ApproverDepartment)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.ApproverPosition)
            .Include(i => i.Actions)
            .OrderBy(i => i.Id)
            .AsSplitQuery();

    public async Task<bool> IsCallerEligibleAsync(
        ApprovalApplicationType type, Guid applicationId, Guid callerEmployeeId, CancellationToken token = default)
    {
        var instance = await GetInstanceReadOnlyAsync(type, applicationId, token);
        if (instance is null || instance.Status != ApprovalInstanceStatus.InProgress) return false;

        var caller = await _uow.Repository.Find<Employee>(e => e.Id == callerEmployeeId).AsNoTracking().FirstOrDefaultAsync(token);
        if (caller is null) return false;
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == instance.ApplicantEmployeeId).AsNoTracking().FirstOrDefaultAsync(token);
        if (applicant is null) return false;

        var delegators = await LoadDelegatorsAsync(callerEmployeeId, type, token);
        return (await GetStandingAsync(instance, caller, applicant, delegators, token)).Eligible;
    }

    /// <summary>
    /// Who the instance's current step points at for its applicant, when that step is one of the
    /// dynamic types (manager level, a head...). Null when the step is a fixed person/department/
    /// position or there is no step (the single-step fallback).
    /// </summary>
    public async Task<ResolvedApprovers?> ResolveCurrentApproversAsync(
        ApprovalInstance instance, Employee applicant, CancellationToken token)
    {
        var step = CurrentStep(instance);
        if (step == null || !ApprovalApproverResolver.IsDynamic(step.ApproverType)) return null;
        return await _approvers.ResolveAsync(step, applicant, token);
    }

    /// <summary>The same for any step of a workflow (labels in the timeline), loading the applicant itself.</summary>
    public async Task<ResolvedApprovers?> ResolveStepApproversAsync(
        ApprovalWorkflowStep step, Guid applicantEmployeeId, CancellationToken token)
    {
        if (!ApprovalApproverResolver.IsDynamic(step.ApproverType)) return null;
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == applicantEmployeeId).AsNoTracking().FirstOrDefaultAsync(token);
        return applicant == null ? null : await _approvers.ResolveAsync(step, applicant, token);
    }

    /// <summary>Resolves the current step's dynamic approvers, then decides the caller's standing.</summary>
    public async Task<ApprovalStanding> GetStandingAsync(
        ApprovalInstance instance, Employee caller, Employee applicant, IReadOnlyCollection<Employee> delegators, CancellationToken token)
    {
        var step = CurrentStep(instance);
        if (step != null && ApprovalStepPools.IsParallel(step))
            return await GetParallelStandingAsync(instance, step, caller, applicant, delegators, token);

        var resolved = await ResolveCurrentApproversAsync(instance, applicant, token);
        return GetStanding(instance, caller, applicant, delegators, resolved?.EmployeeIds);
    }

    /// <summary>Who one pool's dynamic approver resolves to for the applicant; null for a fixed person/department/position.</summary>
    public async Task<ResolvedApprovers?> ResolvePoolApproversAsync(
        ApprovalWorkflowStep effective, Employee applicant, CancellationToken token) =>
        ApprovalApproverResolver.IsDynamic(effective.ApproverType)
            ? await _approvers.ResolveAsync(effective, applicant, token)
            : null;

    /// <summary>
    /// The caller's standing on the first step after the current one that they (or a delegator) would
    /// approve -- "it's coming to me, just not yet". None when no later step names them. Reassignment and
    /// escalation only ever apply to the current step, so they play no part here.
    /// </summary>
    public async Task<ApprovalStanding> GetLaterStepStandingAsync(
        ApprovalInstance instance, Employee caller, Employee applicant, IReadOnlyCollection<Employee> delegators, CancellationToken token)
    {
        var laterSteps = (instance.Workflow?.Steps ?? [])
            .Where(s => s.StepNumber > instance.CurrentStepNumber)
            .OrderBy(s => s.StepNumber);
        foreach (var step in laterSteps)
        {
            foreach (var pool in ApprovalStepPools.Of(step))
            {
                var resolved = await ResolvePoolApproversAsync(pool.Effective, applicant, token);
                var named = pool.GroupId == null ? step.NamedApprovers.Select(a => a.EmployeeId).ToArray() : [];
                var standing = _eligibility.Resolve(new ApprovalEligibilityContext(
                    caller, applicant, pool.Effective, named, null, delegators, resolved?.EmployeeIds));
                if (standing.Eligible) return standing;
            }
        }
        return ApprovalStanding.None;
    }

    // A step with several pools: the caller is eligible when they could act for a pool that still
    // needs approvals, and their approval will count for that pool. A reassigned approver or an
    // escalation person decides for the whole step (the admin escape hatches must be able to unblock it).
    private async Task<ApprovalStanding> GetParallelStandingAsync(
        ApprovalInstance instance, ApprovalWorkflowStep step, Employee caller, Employee applicant,
        IReadOnlyCollection<Employee> delegators, CancellationToken token)
    {
        if (instance.EscalatedToEmployeeId == caller.Id)
            return ApprovalStanding.Escalated with { ClearsWholeStep = true };
        if (instance.ReassignedApproverEmployeeId is { } reassignedId)
            return caller.Id == reassignedId ? ApprovalStanding.Direct with { ClearsWholeStep = true } : ApprovalStanding.None;

        var progress = await PoolProgressAsync(instance, step, applicant, token);
        foreach (var entry in progress.Where(p => !p.Satisfied))
        {
            var pool = entry.Pool;
            IReadOnlyCollection<Guid>? resolved = ApprovalApproverResolver.IsDynamic(pool.Effective.ApproverType)
                ? (await _approvers.ResolveAsync(pool.Effective, applicant, token)).EmployeeIds
                : null;
            var named = pool.GroupId == null ? step.NamedApprovers.Select(a => a.EmployeeId).ToArray() : [];
            var standing = _eligibility.Resolve(new ApprovalEligibilityContext(
                caller, applicant, pool.Effective, named, null, delegators, resolved));
            if (standing.Eligible) return standing with { PoolId = pool.GroupId };
        }
        return ApprovalStanding.None;
    }

    /// <summary>Pool progress of the instance's current step; null when it is an ordinary single-pool step.</summary>
    public async Task<List<PoolProgress>?> CurrentPoolProgressAsync(ApprovalInstance instance, CancellationToken token)
    {
        var step = CurrentStep(instance);
        if (step == null || !ApprovalStepPools.IsParallel(step) || instance.Status != ApprovalInstanceStatus.InProgress) return null;
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == instance.ApplicantEmployeeId).AsNoTracking().FirstOrDefaultAsync(token);
        return applicant == null ? null : await PoolProgressAsync(instance, step, applicant, token);
    }

    /// <summary>
    /// Where each pool of the current step stands: approvals so far this cycle, how many it needs, and
    /// whether it was dropped (a per-applicant approver nobody could be found for, on a pool that skips).
    /// </summary>
    public async Task<List<PoolProgress>> PoolProgressAsync(
        ApprovalInstance instance, ApprovalWorkflowStep step, Employee applicant, CancellationToken token)
    {
        var pools = ApprovalStepPools.Of(step);
        var dropped = await DroppedPoolIdsAsync(pools, applicant, token);
        var thisStep = instance.Actions.Where(a => a.StepNumber == step.StepNumber && a.CycleNumber == instance.CycleNumber);
        return ApprovalStepPools.Progress(pools, thisStep, dropped);
    }

    // Pools set to skip whose per-applicant approver can't be found: they no longer count.
    private async Task<HashSet<Guid>> DroppedPoolIdsAsync(
        IReadOnlyList<ApprovalStepPool> pools, Employee applicant, CancellationToken token)
    {
        var dropped = new HashSet<Guid>();
        foreach (var pool in pools)
        {
            if (pool.Effective.OnUnresolved == UnresolvedApproverPolicy.Skip &&
                ApprovalApproverResolver.IsDynamic(pool.Effective.ApproverType) &&
                (await _approvers.ResolveAsync(pool.Effective, applicant, token)).Unresolved)
                dropped.Add(pool.GroupId ?? Guid.Empty);
        }
        return dropped;
    }

    /// <summary>
    /// Whether the caller may act on the instance's current step -- as themself, or for a delegator.
    /// Pure over what the caller already loaded, so a list screen can evaluate many instances
    /// without a query per row.
    /// </summary>
    public ApprovalStanding GetStanding(
        ApprovalInstance instance, Employee caller, Employee applicant, IReadOnlyCollection<Employee> delegators,
        IReadOnlyCollection<Guid>? resolvedApprovers = null)
    {
        var currentStep = CurrentStep(instance);
        var namedApproverIds = currentStep?.NamedApprovers.Select(a => a.EmployeeId).ToArray() ?? [];
        return _eligibility.Resolve(new ApprovalEligibilityContext(
            caller, applicant, currentStep, namedApproverIds, instance.ReassignedApproverEmployeeId, delegators, resolvedApprovers,
            instance.EscalatedToEmployeeId));
    }

    /// <summary>The employees who have delegated their approvals of this type to the caller right now.</summary>
    public async Task<IReadOnlyCollection<Employee>> LoadDelegatorsAsync(
        Guid callerEmployeeId, ApprovalApplicationType type, CancellationToken token)
    {
        if (_delegations == null) return [];
        var ids = await _delegations.GetActiveDelegatorIdsAsync(callerEmployeeId, type, token);
        if (ids.Count == 0) return [];
        return await _uow.Repository.Find<Employee>(e => ids.Contains(e.Id)).AsNoTracking().ToListAsync(token);
    }

    // callerHasOverrideAccess: true for Owner/Admin, who may act on any pending step regardless
    // of assignment -- resolved by the caller (controller layer) from the same role/claims check
    // that already grants the coarse {Row}:Approve permission broadly, not by this service.
    //
    // applicantEmployeeId is only used to self-heal: an application filed before this engine
    // shipped (or otherwise missing its StartAsync call) has no ApprovalInstance row yet, so one
    // is lazily created here instead of failing the approve/decline action outright -- existing
    // in-flight requests keep working with no backfill migration required.
    public async Task<ApprovalActionResult> RecordActionAsync(
        ApprovalApplicationType type,
        Guid applicationId,
        Guid applicantEmployeeId,
        Guid callerEmployeeId,
        bool callerHasOverrideAccess,
        ApprovalActionType action,
        string? note,
        CancellationToken token = default)
    {
        var instance = await GetInstanceAsync(type, applicationId, token);
        // Self-healed instances are already tracked as Added (StartAsync's own AddAsync call) --
        // calling Repository.Update on them below would downgrade that tracked state to Modified,
        // which skips the INSERT entirely (the row is never actually written), silently
        // orphaning the ApprovalAction row added further down (FOREIGN KEY constraint failed on
        // ApprovalInstanceId -- caught by PayrollBatchLifecycleServiceTests' real-SQLite
        // coverage). Guard every Update(instance) call below on NOT being this self-heal path;
        // the existing-instance path's own Update() calls are untouched.
        var isNewInstance = instance == null;
        instance ??= await StartAsync(type, applicationId, applicantEmployeeId, token, callerEmployeeId);
        if (instance.Status != ApprovalInstanceStatus.InProgress)
            throw new InvalidOperationException("This application is no longer awaiting approval.");

        var caller = await _uow.Repository.Find<Employee>(e => e.Id == callerEmployeeId).AsNoTracking().FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Approver not found.");
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == instance.ApplicantEmployeeId).AsNoTracking().FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Applicant not found.");

        var currentStep = CurrentStep(instance);

        // Idempotency guard: a UI double-click or a client retry after a dropped response can
        // send the same actor's action for the same step twice. ApprovalQuorum.IsStepCleared
        // already de-dupes actors via .Distinct() so a repeat can't inflate quorum, but without
        // this check it would still insert a duplicate ApprovalAction row and -- if quorum was
        // exactly cleared by the first call -- re-run the step-advance/resolution logic and
        // publish duplicate notifications. Scoped to CurrentStepNumber + CycleNumber together
        // (not "ever acted on this instance"), so this only catches a repeat within THIS cycle's
        // current step: a legitimate later action by the same person on a step the instance has
        // since moved on to still goes through normally, and so does a fresh cycle after a
        // restart (StartAsync's restart branch bumps CycleNumber but reuses StepNumber=1, and
        // deliberately keeps the prior cycle's ApprovalAction rows as audit history -- without
        // CycleNumber in this filter, a re-request from the same original approver would be
        // wrongly treated as a duplicate of their action from the cycle that already got
        // declined). Excludes Reassigned since that's a different kind of event (an admin may
        // legitimately reassign more than once), and applies regardless of
        // callerHasOverrideAccess -- an override doesn't make a repeat submission any less of a
        // repeat.
        //
        // Delegation: a delegate acts "as" the delegator, so the guard and the quorum below count
        // the delegator -- a person who has already decided (themself or through their delegate)
        // can't count twice.
        var delegators = await LoadDelegatorsAsync(callerEmployeeId, type, token);
        var standing = await GetStandingAsync(instance, caller, applicant, delegators, token);
        var onBehalfOf = standing.OnBehalfOfEmployeeId;
        var callerKey = onBehalfOf ?? callerEmployeeId;
        var alreadyActedOnCurrentStep = instance.Actions.Any(a =>
            a.StepNumber == instance.CurrentStepNumber &&
            a.CycleNumber == instance.CycleNumber &&
            (a.OnBehalfOfEmployeeId ?? a.ActorEmployeeId) == callerKey &&
            a.Action != ApprovalActionType.Reassigned);
        if (alreadyActedOnCurrentStep)
            return new ApprovalActionResult(instance.Status, instance.CurrentStepNumber);

        if (!callerHasOverrideAccess && !standing.Eligible)
            throw new UnauthorizedAccessException("You are not an eligible approver for this step.");

        var noteRequirement = currentStep?.NoteRequirement ?? NoteRequirement.None;
        if (noteRequirement == NoteRequirement.Required && string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("A note is required to act on this step.");

        // With several pools on the step, an approval counts for the pool the caller acted for; an admin
        // override, a reassigned approver or an escalation decides for the whole step.
        var parallel = currentStep != null && ApprovalStepPools.IsParallel(currentStep);
        Guid? groupId = !parallel ? null
            : callerHasOverrideAccess || standing.ClearsWholeStep ? ApprovalStepPools.WholeStep
            : standing.PoolId;
        var recorded = new ApprovalAction
        {
            ApprovalInstanceId = instance.Id,
            StepNumber = instance.CurrentStepNumber,
            CycleNumber = instance.CycleNumber,
            ActorEmployeeId = callerEmployeeId,
            OnBehalfOfEmployeeId = onBehalfOf,
            GroupId = groupId,
            Action = action,
            Note = noteRequirement == NoteRequirement.None ? null : note,
        };
        await _uow.Repository.AddAsync(recorded, token);
        await AuditActionAsync(instance, applicant, action, noteRequirement == NoteRequirement.None ? null : note, onBehalfOf, token);

        if (action == ApprovalActionType.Declined)
        {
            instance.Status = ApprovalInstanceStatus.Declined;
            ClearStepAssignments(instance);
            if (!isNewInstance) _uow.Repository.Update(instance);
            await PublishResolutionNotificationAsync(instance, applicant, callerEmployeeId, note, token);
            await OnInstanceClosedAsync(instance, token);
            return new ApprovalActionResult(instance.Status, instance.CurrentStepNumber);
        }

        var minApprovals = currentStep?.MinApprovals ?? 1;
        // Current cycle only -- a restarted chain (edit after partial approval, or a re-request)
        // keeps prior cycles' actions as audit history, and those must not count toward quorum.
        var approverIdsForStep = instance.Actions
            .Where(a => a.StepNumber == instance.CurrentStepNumber
                && a.CycleNumber == instance.CycleNumber
                && a.Action == ApprovalActionType.Approved)
            .Select(a => a.OnBehalfOfEmployeeId ?? a.ActorEmployeeId)
            .OfType<Guid>()
            .Append(callerKey); // the action added above isn't in instance.Actions yet

        // A step with several pools clears only when every pool has its own quorum (the action just
        // recorded included); an ordinary step counts distinct approvers as before.
        bool stepCleared;
        if (parallel)
        {
            var pools = ApprovalStepPools.Of(currentStep!);
            var dropped = await DroppedPoolIdsAsync(pools, applicant, token);
            var thisStep = instance.Actions
                .Where(a => a.StepNumber == instance.CurrentStepNumber && a.CycleNumber == instance.CycleNumber)
                .Where(a => a.Id != recorded.Id)
                .Append(recorded);
            stepCleared = ApprovalStepPools.IsCleared(ApprovalStepPools.Progress(pools, thisStep, dropped));
        }
        else
        {
            stepCleared = ApprovalQuorum.IsStepCleared(minApprovals, approverIdsForStep);
        }

        if (!stepCleared)
        {
            if (!isNewInstance) _uow.Repository.Update(instance);
            return new ApprovalActionResult(instance.Status, instance.CurrentStepNumber);
        }

        // The next step that actually applies: a step whose approver can't be found and which says
        // "skip" is passed over (recorded as a system entry). None left = the request is approved.
        var nextStepNumber = await FindNextStepNumberAsync(instance, applicant, token);
        if (nextStepNumber is null)
        {
            instance.Status = ApprovalInstanceStatus.Approved;
            ClearStepAssignments(instance);
            if (!isNewInstance) _uow.Repository.Update(instance);
            await PublishResolutionNotificationAsync(instance, applicant, callerEmployeeId, note, token);
            await OnInstanceClosedAsync(instance, token);
        }
        else
        {
            var completedStepNumber = instance.CurrentStepNumber;
            instance.CurrentStepNumber = nextStepNumber.Value;
            ClearStepAssignments(instance);
            EnterStep(instance);
            if (!isNewInstance) _uow.Repository.Update(instance);
            var nextStep = CurrentStep(instance);
            if (nextStep != null)
                await PublishStepNotificationAsync(instance, nextStep, applicant, callerEmployeeId, token);
            // The applicant used to hear nothing between filing and the final decision, however
            // many steps cleared in between -- tell them each time the chain moves forward.
            await PublishProgressNotificationAsync(instance, applicant, completedStepNumber, callerEmployeeId, note, token);
        }

        return new ApprovalActionResult(instance.Status, instance.CurrentStepNumber);
    }

    /// <summary>
    /// Tells the current step's approvers (and anyone covering for them) it is still waiting. Same
    /// notice as when the step opened, with a note saying how long. Records that a reminder went out,
    /// so the sweep doesn't send it again too soon. Publishes through the outbox: call it before the
    /// caller commits.
    /// </summary>
    public async Task SendReminderAsync(ApprovalInstance instance, Employee applicant, TimeSpan waited, CancellationToken token)
    {
        var step = CurrentStep(instance);
        if (step == null) return;

        await PublishStepNotificationAsync(instance, step, applicant, Guid.Empty, token,
            note: $"Reminder: waiting {ApprovalSlaPolicy.DescribeWait(waited)}");
        instance.LastReminderAt = DateTime.UtcNow;
        instance.ReminderCount += 1;
    }

    /// <summary>
    /// The current step waited past its escalation time: its escalation person may now act as well as
    /// the step's own approvers, and is told. A system entry on the timeline records it. Does nothing
    /// when the step names no one or the person can't be found.
    /// </summary>
    public async Task EscalateAsync(ApprovalInstance instance, Employee applicant, TimeSpan waited, CancellationToken token)
    {
        var step = CurrentStep(instance);
        if (step?.EscalateToEmployeeId is not { } targetId) return;
        var target = await _uow.Repository.Find<Employee>(e => e.Id == targetId).AsNoTracking().FirstOrDefaultAsync(token);
        if (target == null) return;

        var waitedText = ApprovalSlaPolicy.DescribeWait(waited);
        instance.EscalatedToEmployeeId = targetId;
        await _uow.Repository.AddAsync(new ApprovalAction
        {
            ApprovalInstanceId = instance.Id,
            StepNumber = instance.CurrentStepNumber,
            CycleNumber = instance.CycleNumber,
            ActorEmployeeId = null,
            Action = ApprovalActionType.Escalated,
            Note = $"Waiting {waitedText}: escalated to {target.FirstName} {target.LastName}.".Replace("  ", " "),
        }, token);

        if (string.IsNullOrWhiteSpace(target.Email) && target.UserId == null) return;
        var (deliverEmail, deliverPush) = await ResolveDeliveryFlagsAsync(target.Id, instance.ApplicationType, token);
        await _publisher.Publish(new ApprovalNotificationRequested
        {
            RecipientEmail = target.Email ?? string.Empty,
            RecipientName = $"{target.FirstName} {target.LastName}".Trim(),
            ApplicationTypeLabel = ApplicationTypeLabel(instance.ApplicationType),
            ApplicantName = $"{applicant.FirstName} {applicant.LastName}".Trim(),
            StatusLabel = PendingApprovalStatusLabel,
            StepNumber = instance.CurrentStepNumber,
            TotalSteps = instance.Workflow?.Steps.Count ?? 1,
            Note = $"Escalated: waiting {waitedText}",
            ApplicationType = instance.ApplicationType.ToString(),
            ApplicationId = instance.ApplicationId,
            ApprovalInstanceId = instance.Id,
            RecipientUserId = target.UserId,
            DeliverEmail = deliverEmail && !string.IsNullOrWhiteSpace(target.Email),
            DeliverPush = deliverPush && IsPushReachable(target),
        }, token);
    }

    // Per-step state that must not outlive the step: a reassignment and an escalation both belong to the
    // step they were made for.
    private static void ClearStepAssignments(ApprovalInstance instance)
    {
        instance.ReassignedApproverEmployeeId = null;
        instance.EscalatedToEmployeeId = null;
    }

    // The request just reached a step: the reminder/escalation clock starts over.
    private static void EnterStep(ApprovalInstance instance)
    {
        instance.StepEnteredAt = DateTime.UtcNow;
        instance.LastReminderAt = null;
        instance.ReminderCount = 0;
    }

    // The first step after the current one that isn't skipped. A dynamic step set to Skip whose
    // approver can't be found for this applicant is passed over, leaving a Skipped entry (no actor)
    // so the timeline shows it. Null = no step is left, so the request is fully approved.
    private async Task<int?> FindNextStepNumberAsync(ApprovalInstance instance, Employee applicant, CancellationToken token)
    {
        var attributes = ApprovalRequestAttributes.FromJson(instance.AttributesJson);
        var later = (instance.Workflow?.Steps ?? [])
            .Where(s => s.StepNumber > instance.CurrentStepNumber)
            .OrderBy(s => s.StepNumber);
        foreach (var step in later)
        {
            // A step that depends on a measure of the request ("5 days or more") and isn't met is
            // passed over, with the reason on the timeline.
            if (!ApprovalStepCondition.Applies(step, attributes))
            {
                await _uow.Repository.AddAsync(new ApprovalAction
                {
                    ApprovalInstanceId = instance.Id,
                    StepNumber = step.StepNumber,
                    CycleNumber = instance.CycleNumber,
                    ActorEmployeeId = null,
                    Action = ApprovalActionType.Skipped,
                    Note = "Not needed for this request.",
                }, token);
                continue;
            }

            // A step with several pools is passed over only when every pool was dropped (no approver found
            // and set to skip); otherwise the pools that remain are still needed.
            var allPoolsDropped = ApprovalStepPools.IsParallel(step)
                ? (await DroppedPoolIdsAsync(ApprovalStepPools.Of(step), applicant, token)).Count == ApprovalStepPools.Of(step).Count
                : step.OnUnresolved == UnresolvedApproverPolicy.Skip && ApprovalApproverResolver.IsDynamic(step.ApproverType)
                    && (await _approvers.ResolveAsync(step, applicant, token)).Unresolved;
            if (allPoolsDropped)
            {
                await _uow.Repository.AddAsync(new ApprovalAction
                {
                    ApprovalInstanceId = instance.Id,
                    StepNumber = step.StepNumber,
                    CycleNumber = instance.CycleNumber,
                    ActorEmployeeId = null,
                    Action = ApprovalActionType.Skipped,
                    Note = "No approver found for this step.",
                }, token);
                continue;
            }
            return step.StepNumber;
        }
        return null;
    }

    // Owner/Admin-only escape hatch for a step whose configured/resolved approver can't
    // actually act (out of office, left the company, wrongly assigned) -- swaps the CURRENT
    // step's sole eligible approver to a specific employee, without touching the shared
    // ApprovalWorkflow/ApprovalWorkflowStep template (which is permanently frozen once any
    // instance references it -- see ApprovalWorkflowService.EnsureEditableAsync). The override
    // is scoped to exactly this step: RecordActionAsync clears it the moment the step advances
    // or the instance resolves, so it never carries into a step it wasn't meant for. No
    // eligibility check on the act of reassigning itself -- gated entirely by the caller
    // (controller layer) being Owner/Admin, same as callerHasOverrideAccess elsewhere.
    public async Task ReassignApproverAsync(
        ApprovalApplicationType type,
        Guid applicationId,
        Guid newApproverEmployeeId,
        Guid reassignedByEmployeeId,
        string? note,
        CancellationToken token = default)
    {
        var instance = await GetInstanceAsync(type, applicationId, token)
            ?? throw new NotFoundException("No approval instance found for this application.");
        if (instance.Status != ApprovalInstanceStatus.InProgress)
            throw new InvalidOperationException("This application is no longer awaiting approval.");

        var newApprover = await _uow.Repository.Find<Employee>(e => e.Id == newApproverEmployeeId)
            .AsNoTracking().FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Employee not found.");
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == instance.ApplicantEmployeeId)
            .AsNoTracking().FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Applicant not found.");

        instance.ReassignedApproverEmployeeId = newApproverEmployeeId;
        _uow.Repository.Update(instance);

        await _uow.Repository.AddAsync(new ApprovalAction
        {
            ApprovalInstanceId = instance.Id,
            StepNumber = instance.CurrentStepNumber,
            CycleNumber = instance.CycleNumber,
            ActorEmployeeId = reassignedByEmployeeId,
            Action = ApprovalActionType.Reassigned,
            Note = note,
        }, token);

        if (newApprover.Id != reassignedByEmployeeId && (!string.IsNullOrWhiteSpace(newApprover.Email) || newApprover.UserId != null))
        {
            var (deliverEmail, deliverPush) = await ResolveDeliveryFlagsAsync(newApprover.Id, instance.ApplicationType, token);

            await _publisher.Publish(new ApprovalNotificationRequested
            {
                RecipientEmail = newApprover.Email ?? string.Empty,
                RecipientName = $"{newApprover.FirstName} {newApprover.LastName}".Trim(),
                ApplicationTypeLabel = ApplicationTypeLabel(instance.ApplicationType),
                ApplicantName = $"{applicant.FirstName} {applicant.LastName}".Trim(),
                StatusLabel = PendingApprovalStatusLabel,
                StepNumber = instance.CurrentStepNumber,
                TotalSteps = instance.Workflow?.Steps.Count ?? 1,
                ApplicationType = instance.ApplicationType.ToString(),
                ApplicationId = instance.ApplicationId,
                ApprovalInstanceId = instance.Id,
                RecipientUserId = newApprover.UserId,
                DeliverEmail = deliverEmail && !string.IsNullOrWhiteSpace(newApprover.Email),
                DeliverPush = deliverPush && IsPushReachable(newApprover),
            }, token);
        }
    }

    // One audit event per approve/decline click, for every application type -- recorded here,
    // where eligibility has just been confirmed, so it commits with the action itself. The actor
    // (the approver's account) is resolved from the request by the audit pipeline.
    private Task AuditActionAsync(
        ApprovalInstance instance, Employee applicant, ApprovalActionType action, string? note, Guid? onBehalfOf, CancellationToken token)
    {
        if (_audit == null) return Task.CompletedTask;

        var declined = action == ApprovalActionType.Declined;
        var totalSteps = instance.Workflow?.Steps.Count ?? 1;
        var metadata = new Dictionary<string, string?>
        {
            ["Step"] = instance.CurrentStepNumber.ToString(),
            ["TotalSteps"] = totalSteps.ToString(),
        };
        if (!string.IsNullOrWhiteSpace(note)) metadata["Note"] = note;
        if (onBehalfOf is { } delegator) metadata["OnBehalfOfEmployeeId"] = delegator.ToString();

        return _audit.WriteAsync(new AuditEvent
        {
            EventType = declined ? AuditEventTypes.Reject : AuditEventTypes.Approve,
            EntityType = instance.ApplicationType.ToString(),
            EntityId = instance.ApplicationId.ToString(),
            Description = $"{(declined ? "Declined" : "Approved")} {ApplicationTypeLabel(instance.ApplicationType)} " +
                $"of {applicant.FirstName} {applicant.LastName}".TrimEnd() +
                (totalSteps > 1 ? $" (step {instance.CurrentStepNumber} of {totalSteps})" : ""),
            Metadata = metadata,
        }, token);
    }

    private static ApprovalWorkflowStep? CurrentStep(ApprovalInstance instance) =>
        instance.Workflow?.Steps.SingleOrDefault(s => s.StepNumber == instance.CurrentStepNumber);

    // Who a step's ApproverType actually resolves to right now, for notification purposes -- the
    // same resolution rules ApproverEligibilityResolver checks against a single caller, just
    // materialized into the full candidate list here since a notification may need to reach
    // several people (a whole department/position pool) rather than approve/reject one.
    // Everyone to tell about a step: its approver, and -- on a step with several pools -- every pool's,
    // each person once.
    private async Task<List<Employee>> ResolveStepRecipientsAsync(
        ApprovalWorkflowStep step, Employee applicant, CancellationToken token)
    {
        if (!ApprovalStepPools.IsParallel(step)) return await ResolveNotificationRecipientsAsync(step, applicant, token);

        var everyone = new List<Employee>();
        foreach (var pool in ApprovalStepPools.Of(step))
            everyone.AddRange(await ResolveNotificationRecipientsAsync(pool.Effective, applicant, token));
        return everyone.DistinctBy(e => e.Id).ToList();
    }

    private async Task<List<Employee>> ResolveNotificationRecipientsAsync(
        ApprovalWorkflowStep step, Employee applicant, CancellationToken token)
    {
        // Pool types (Department/Position/Applicant's Department) include anyone reachable -- a
        // linked account OR an email. They used to require a linked UserId, which silently left out
        // every account matched to its employee by email (e.g. an Owner who never came through an
        // invitation), even though those people can approve the step.
        List<Employee> candidates;
        switch (step.ApproverType)
        {
            case ApproverType.Person:
                candidates = step.ApproverEmployeeId is { } personId
                    ? await _uow.Repository.Find<Employee>(e => e.Id == personId).AsNoTracking().ToListAsync(token)
                    : [];
                break;
            case ApproverType.Department:
                candidates = step.ApproverDepartmentId is { } deptId
                    ? await _uow.Repository.Find<Employee>(e => e.DepartmentId == deptId && (e.UserId != null || (e.Email != null && e.Email != ""))).AsNoTracking().ToListAsync(token)
                    : [];
                break;
            case ApproverType.Position:
                candidates = step.ApproverPositionId is { } posId
                    ? await _uow.Repository.Find<Employee>(e => e.PositionId == posId && (e.UserId != null || (e.Email != null && e.Email != ""))).AsNoTracking().ToListAsync(token)
                    : [];
                break;
            case ApproverType.ApplicantManager:
            case ApproverType.ApplicantManagerLevel:
            case ApproverType.ApplicantDepartmentHead:
            case ApproverType.ApplicantSectionHead:
            case ApproverType.ApplicantBranchHead:
            case ApproverType.ApplicantAreaHead:
            case ApproverType.ApplicantClientHead:
            {
                var resolved = (await _approvers.ResolveAsync(step, applicant, token)).EmployeeIds;
                candidates = resolved.Count == 0
                    ? []
                    : await _uow.Repository.Find<Employee>(e => resolved.Contains(e.Id)).AsNoTracking().ToListAsync(token);
                break;
            }
            case ApproverType.ApplicantDepartment:
                candidates = applicant.DepartmentId is { } applicantDeptId
                    ? await _uow.Repository.Find<Employee>(e => e.DepartmentId == applicantDeptId && (e.UserId != null || (e.Email != null && e.Email != ""))).AsNoTracking().ToListAsync(token)
                    : [];
                break;
            default:
                candidates = [];
                break;
        }

        var namedIds = step.NamedApprovers.Select(a => a.EmployeeId).ToHashSet();
        if (namedIds.Count > 0)
            candidates = candidates.Where(c => namedIds.Contains(c.Id)).ToList();

        // Reachable via at least one channel -- email, push (needs a linked portal login), or
        // both. A candidate with neither is filtered out since no notification could ever reach
        // them either way.
        return candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.Email) || c.UserId != null)
            .ToList();
    }

    private async Task PublishStepNotificationAsync(
        ApprovalInstance instance, ApprovalWorkflowStep step, Employee applicant, Guid excludeEmployeeId, CancellationToken token,
        string? note = null)
    {
        // Never the person who just filed or acted: they caused this, so it isn't news to them.
        var resolved = await ResolveStepRecipientsAsync(step, applicant, token);
        var recipients = resolved
            .Where(c => c.Id != excludeEmployeeId)
            .Select(c => (Recipient: c, CoveringFor: (string?)null))
            .ToList();
        var totalSteps = instance.Workflow?.Steps.Count ?? 1;
        var applicantName = $"{applicant.FirstName} {applicant.LastName}".Trim();

        // People covering for an approver are told too, with a note saying whose queue it is.
        recipients.AddRange(await ResolveDelegateRecipientsAsync(resolved, instance.ApplicationType, excludeEmployeeId, token));

        foreach (var (recipient, coveringFor) in recipients)
        {
            var (deliverEmail, deliverPush) = await ResolveDeliveryFlagsAsync(recipient.Id, instance.ApplicationType, token);

            await _publisher.Publish(new ApprovalNotificationRequested
            {
                RecipientEmail = recipient.Email ?? string.Empty,
                RecipientName = $"{recipient.FirstName} {recipient.LastName}".Trim(),
                ApplicationTypeLabel = ApplicationTypeLabel(instance.ApplicationType),
                ApplicantName = applicantName,
                StatusLabel = PendingApprovalStatusLabel,
                StepNumber = instance.CurrentStepNumber,
                TotalSteps = totalSteps,
                Note = note ?? (coveringFor == null ? null : $"Delegated from {coveringFor}"),
                ApplicationType = instance.ApplicationType.ToString(),
                ApplicationId = instance.ApplicationId,
                ApprovalInstanceId = instance.Id,
                RecipientUserId = recipient.UserId,
                DeliverEmail = deliverEmail && !string.IsNullOrWhiteSpace(recipient.Email),
                DeliverPush = deliverPush && IsPushReachable(recipient),
            }, token);
        }
    }

    // The people covering, right now, for any of these approvers -- each paired with the name of
    // the approver they cover. Anyone already addressed as an approver in their own right, and
    // whoever caused the event, is left out.
    private async Task<List<(Employee Recipient, string? CoveringFor)>> ResolveDelegateRecipientsAsync(
        IReadOnlyCollection<Employee> approvers, ApprovalApplicationType type, Guid excludeEmployeeId, CancellationToken token)
    {
        if (_delegations == null || approvers.Count == 0) return [];
        var pairs = await _delegations.GetActiveDelegatesAsync(approvers.Select(a => a.Id).ToList(), type, token);
        if (pairs.Count == 0) return [];

        var alreadyAddressed = approvers.Select(a => a.Id).Append(excludeEmployeeId).ToHashSet();
        var delegateIds = pairs.Select(p => p.DelegateId).Where(id => !alreadyAddressed.Contains(id)).Distinct().ToList();
        if (delegateIds.Count == 0) return [];

        var delegates = await _uow.Repository.Find<Employee>(e => delegateIds.Contains(e.Id)).AsNoTracking().ToListAsync(token);
        var result = new List<(Employee, string?)>();
        foreach (var employee in delegates.Where(e => !string.IsNullOrWhiteSpace(e.Email) || e.UserId != null))
        {
            var delegator = pairs.Where(p => p.DelegateId == employee.Id)
                .Select(p => approvers.FirstOrDefault(a => a.Id == p.DelegatorId))
                .FirstOrDefault(a => a != null);
            result.Add((employee, delegator == null ? null : $"{delegator.FirstName} {delegator.LastName}".Trim()));
        }
        return result;
    }

    /// <summary>StatusLabel for "an approver needs to act" -- the frontend keys its
    /// "…is waiting on you" message off this exact value.</summary>
    public const string PendingApprovalStatusLabel = "Pending Your Approval";

    // No workflow configured (implicit fallback step): there's no named approver to address, and
    // who holds {Row}:Approve is only known from JWT claims -- so hand it to the push-only approver
    // group instead (see ApproverGroups). Without this, filing under an unconfigured type notified
    // nobody at all.
    private Task PublishPoolNotificationAsync(ApprovalInstance instance, Employee applicant, CancellationToken token) =>
        _publisher.Publish(new ApprovalPoolNotificationRequested
        {
            ApplicationType = instance.ApplicationType,
            ApplicationId = instance.ApplicationId,
            ApprovalInstanceId = instance.Id,
            ApplicationTypeLabel = ApplicationTypeLabel(instance.ApplicationType),
            ApplicantName = $"{applicant.FirstName} {applicant.LastName}".Trim(),
        }, token);

    /// <summary>StatusLabel for an intermediate step clearing on a multi-step chain (the
    /// instance is still InProgress). StepNumber is the step that was just approved.</summary>
    public const string StepApprovedStatusLabel = "Step Approved";

    // Applicant-facing "your application moved forward" notice for a multi-step chain -- sent
    // when an intermediate step clears (the final step goes through
    // PublishResolutionNotificationAsync instead). Same channels/preferences as the resolution
    // notice, so an applicant who opted out of email for this type still gets the push.
    private async Task PublishProgressNotificationAsync(
        ApprovalInstance instance, Employee applicant, int completedStepNumber, Guid actorEmployeeId, string? note, CancellationToken token)
    {
        if (applicant.Id == actorEmployeeId) return; // approving your own request isn't news to you
        if (string.IsNullOrWhiteSpace(applicant.Email) && applicant.UserId == null) return;

        var (deliverEmail, deliverPush) = await ResolveDeliveryFlagsAsync(applicant.Id, instance.ApplicationType, token);
        var applicantName = $"{applicant.FirstName} {applicant.LastName}".Trim();

        await _publisher.Publish(new ApprovalNotificationRequested
        {
            RecipientEmail = applicant.Email ?? string.Empty,
            RecipientName = applicantName,
            ApplicationTypeLabel = ApplicationTypeLabel(instance.ApplicationType),
            ApplicantName = applicantName,
            StatusLabel = StepApprovedStatusLabel,
            StepNumber = completedStepNumber,
            TotalSteps = instance.Workflow?.Steps.Count ?? 1,
            Note = note,
            ApplicationType = instance.ApplicationType.ToString(),
            ApplicationId = instance.ApplicationId,
            ApprovalInstanceId = instance.Id,
            RecipientUserId = applicant.UserId,
            DeliverEmail = deliverEmail && !string.IsNullOrWhiteSpace(applicant.Email),
            DeliverPush = deliverPush && IsPushReachable(applicant),
        }, token);
    }

    /// <summary>StatusLabel for an approved application being revoked afterwards.</summary>
    public const string CancelledStatusLabel = "Cancelled";

    /// <summary>
    /// Tells the applicant their application's status changed outside the approval chain (e.g.
    /// an approved pass slip revoked) -- same channels and notification preferences as the
    /// approve/decline notice. Publishes through the outbox, so call it before the caller commits.
    /// <paramref name="allowEmail"/> false keeps it to the bell/push (the background DTR save's
    /// outcome notices aren't worth an email each).
    /// </summary>
    public async Task NotifyApplicantAsync(
        ApprovalApplicationType type, Guid applicationId, Guid applicantEmployeeId, string statusLabel, string? note,
        CancellationToken token = default, bool allowEmail = true, Guid? actorEmployeeId = null)
    {
        if (actorEmployeeId == applicantEmployeeId) return; // the person who caused it isn't told about it
        var applicant = await _uow.Repository.Find<Employee>(e => e.Id == applicantEmployeeId)
            .AsNoTracking().FirstOrDefaultAsync(token);
        if (applicant == null || (string.IsNullOrWhiteSpace(applicant.Email) && applicant.UserId == null)) return;

        var instance = await GetInstanceReadOnlyAsync(type, applicationId, token);
        var (deliverEmail, deliverPush) = await ResolveDeliveryFlagsAsync(applicant.Id, type, token);
        var applicantName = $"{applicant.FirstName} {applicant.LastName}".Trim();

        await _publisher.Publish(new ApprovalNotificationRequested
        {
            RecipientEmail = applicant.Email ?? string.Empty,
            RecipientName = applicantName,
            ApplicationTypeLabel = ApplicationTypeLabel(type),
            ApplicantName = applicantName,
            StatusLabel = statusLabel,
            Note = note,
            ApplicationType = type.ToString(),
            ApplicationId = applicationId,
            ApprovalInstanceId = instance?.Id ?? Guid.Empty,
            RecipientUserId = applicant.UserId,
            DeliverEmail = allowEmail && deliverEmail && !string.IsNullOrWhiteSpace(applicant.Email),
            DeliverPush = deliverPush && IsPushReachable(applicant),
        }, token);
    }

    private async Task PublishResolutionNotificationAsync(
        ApprovalInstance instance, Employee applicant, Guid actorEmployeeId, string? note, CancellationToken token)
    {
        if (applicant.Id == actorEmployeeId) return; // deciding your own request isn't news to you
        if (string.IsNullOrWhiteSpace(applicant.Email) && applicant.UserId == null) return;

        var (deliverEmail, deliverPush) = await ResolveDeliveryFlagsAsync(applicant.Id, instance.ApplicationType, token);

        await _publisher.Publish(new ApprovalNotificationRequested
        {
            RecipientEmail = applicant.Email ?? string.Empty,
            RecipientName = $"{applicant.FirstName} {applicant.LastName}".Trim(),
            ApplicationTypeLabel = ApplicationTypeLabel(instance.ApplicationType),
            ApplicantName = $"{applicant.FirstName} {applicant.LastName}".Trim(),
            StatusLabel = instance.Status == ApprovalInstanceStatus.Approved ? "Approved" : "Declined",
            Note = note,
            ApplicationType = instance.ApplicationType.ToString(),
            ApplicationId = instance.ApplicationId,
            ApprovalInstanceId = instance.Id,
            RecipientUserId = applicant.UserId,
            DeliverEmail = deliverEmail && !string.IsNullOrWhiteSpace(applicant.Email),
            DeliverPush = deliverPush && IsPushReachable(applicant),
        }, token);
    }

    // No row = both channels on -- opt-out model, avoids a backfill migration; see
    // NotificationPreference's own doc comment. Queried directly via _uow rather than through
    // NotificationPreferenceService so this engine's constructor doesn't pick up an extra
    // dependency purely for this lookup -- that service exists for the employee-facing settings
    // CRUD (get/update own preferences), a different concern from this internal resolution.
    private async Task<(bool Email, bool Push)> ResolveDeliveryFlagsAsync(
        Guid employeeId, ApprovalApplicationType applicationType, CancellationToken token)
    {
        var preference = await _uow.Repository
            .Find<NotificationPreference>(x => x.EmployeeId == employeeId && x.ApplicationType == applicationType)
            .AsNoTracking()
            .FirstOrDefaultAsync(token);
        return preference == null ? (true, true) : (preference.EmailEnabled, preference.PushEnabled);
    }

    // A push reaches the person through their login email (ApprovalRecipientGroups) or, when the
    // account was linked by invitation, their UserId -- only the UserId used to count, which left
    // every employee matched to their account by email without live updates.
    private static bool IsPushReachable(Employee employee) =>
        employee.UserId != null || !string.IsNullOrWhiteSpace(employee.Email);
    private static string ApplicationTypeLabel(ApprovalApplicationType type) => type switch
    {
        ApprovalApplicationType.Leave => "Leave",
        ApprovalApplicationType.Overtime => "Overtime",
        ApprovalApplicationType.OfficialBusiness => "Official Business",
        ApprovalApplicationType.PassSlip => "Pass Slip",
        ApprovalApplicationType.Loan => "Loan/Deduction",
        ApprovalApplicationType.ProfileUpdate => "Profile Update",
        ApprovalApplicationType.PayrollPosting => "Payroll Posting",
        ApprovalApplicationType.Dtr => "DTR Posting",
        ApprovalApplicationType.DtrDeletion => "DTR Deletion",
        ApprovalApplicationType.PayrollPostingDeletion => "Payroll Posting Deletion",
        _ => type.ToString(),
    };

    // Maps the engine's own instance status back onto each application's pre-existing
    // ApprovalStatus enum, so every current report/query that reads that field keeps working
    // unchanged. InProgress means "still pending, just possibly advanced a step" -- there's no
    // partial-progress value on the legacy enum, so it stays ForApproval.
    public static ApprovalStatus MapInstanceStatus(ApprovalInstanceStatus status) => status switch
    {
        ApprovalInstanceStatus.Approved => ApprovalStatus.Approved,
        ApprovalInstanceStatus.Declined => ApprovalStatus.Declined,
        ApprovalInstanceStatus.Cancelled => ApprovalStatus.Cancelled,
        _ => ApprovalStatus.ForApproval,
    };
}
