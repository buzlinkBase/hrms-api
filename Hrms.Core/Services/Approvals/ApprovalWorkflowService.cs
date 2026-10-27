using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Core.Services.Approvals;

/// <summary>How much a workflow is in use: requests still going through it, and ever.</summary>
public sealed record WorkflowUsage(int InProgress, bool HasBeenUsed)
{
    public static readonly WorkflowUsage None = new(0, false);
    /// <summary>Steps may change only while no request is running on them.</summary>
    public bool CanEdit => InProgress == 0;
    /// <summary>Past requests reference the workflow, so only a never-used one can be deleted.</summary>
    public bool CanDelete => !HasBeenUsed;
}

/// <summary>An active workflow considered for an employee, and whether the employee matches it.</summary>
public sealed record WorkflowCandidate(ApprovalWorkflow Workflow, bool Matched);

/// <summary>The workflow an employee's applications run on (null = the single-step fallback) and the ones considered.</summary>
public sealed record WorkflowResolution(ApprovalWorkflow? Winner, List<WorkflowCandidate> Candidates);

// CRUD for the saved policy (ApprovalWorkflow/ApprovalWorkflowStep/ApprovalWorkflowStepApprover)
// that the Setup > Approval Workflows screen manages. ApprovalEngineService is the separate
// runtime piece that actually walks an application through whatever this service has saved.
//
// Requests read their workflow's steps live while in progress (who's next, step N of M), so a
// workflow can't change while any request is still running on it. Finished requests keep their
// own record of who decided what, so once nothing is in progress the workflow may be edited --
// the change applies to the next applications. To change it while requests keep coming, copy it
// as a new version and either activate that now or schedule it to take over once the current
// one drains (ScheduleActivationAsync).
public class ApprovalWorkflowService : BaseService<ApprovalWorkflow>
{
    public ApprovalWorkflowService(IUnitOfWorkService uow) : base(uow) { }

    public async Task<List<ApprovalWorkflow>> ListAsync(ApprovalApplicationType type, CancellationToken token = default) =>
        await Context.ApprovalWorkflows
            .Include(w => w.Steps.OrderBy(s => s.StepNumber)).ThenInclude(s => s.NamedApprovers)
            .Include(w => w.Steps).ThenInclude(s => s.Groups)
            .Include(w => w.Scopes)
            .Include(w => w.ScopeDepartment)
            .Where(w => w.ApplicationType == type)
            .OrderByDescending(w => w.IsActive).ThenBy(w => w.Name)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(token);

    public async Task<ApprovalWorkflow?> GetAsync(Guid id, CancellationToken token = default) =>
        await Context.ApprovalWorkflows
            .Include(w => w.Steps.OrderBy(s => s.StepNumber)).ThenInclude(s => s.NamedApprovers)
            .Include(w => w.Steps).ThenInclude(s => s.Groups)
            .Include(w => w.Scopes)
            .Include(w => w.ScopeDepartment)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, token);

    public async Task<bool> HasInstancesAsync(Guid workflowId, CancellationToken token = default) =>
        await Context.IgnoringConfidentiality<ApprovalInstance>().AnyAsync(i => i.ApprovalWorkflowId == workflowId, token);

    public Task<int> CountInProgressAsync(Guid workflowId, CancellationToken token = default) =>
        ApprovalWorkflowSwitch.CountInProgressAsync(Context, workflowId, excludingInstanceId: null, token);

    /// <summary>Usage of each workflow in one grouped query (the list screen's Edit/View/Delete state).</summary>
    public async Task<Dictionary<Guid, WorkflowUsage>> GetUsageAsync(IReadOnlyCollection<Guid> workflowIds, CancellationToken token = default)
    {
        var ids = workflowIds.ToList();
        var counts = await Context.IgnoringConfidentiality<ApprovalInstance>()
            .Where(i => i.ApprovalWorkflowId != null && ids.Contains(i.ApprovalWorkflowId.Value))
            .GroupBy(i => i.ApprovalWorkflowId!.Value)
            .Select(g => new
            {
                WorkflowId = g.Key,
                InProgress = g.Count(i => i.Status == ApprovalInstanceStatus.InProgress),
            })
            .ToListAsync(token);
        return ids.ToDictionary(
            id => id,
            id => counts.FirstOrDefault(c => c.WorkflowId == id) is { } c
                ? new WorkflowUsage(c.InProgress, HasBeenUsed: true)
                : WorkflowUsage.None);
    }

    public async Task<ApprovalWorkflow> CreateWorkflowAsync(ApprovalWorkflow workflow, CancellationToken token = default)
    {
        ValidateSteps(workflow.Steps, workflow.ApplicationType);
        ValidateScope(workflow);
        await CreateAsync(workflow, token);
        await CommitChangesAsync(token);
        return workflow;
    }

    // Replaces a workflow's Name/Scope/Steps wholesale -- the Setup screen's Edit action. Refused
    // while any request is still running on it (EnsureEditableAsync). Editing a version that was
    // scheduled to take over cancels that schedule: it gets re-scheduled deliberately, once final.
    public async Task<ApprovalWorkflow> UpdateWorkflowAsync(Guid workflowId, ApprovalWorkflow updated, CancellationToken token = default)
    {
        await EnsureEditableAsync(workflowId, token);
        ValidateSteps(updated.Steps, updated.ApplicationType);
        ValidateScope(updated);

        var existing = await Context.ApprovalWorkflows
            .Include(w => w.Steps)
            .ThenInclude(s => s.NamedApprovers)
            .Include(w => w.Steps).ThenInclude(s => s.Groups)
            .Include(w => w.Scopes)
            .FirstOrDefaultAsync(w => w.Id == workflowId, token)
            ?? throw new NotFoundException("Workflow not found.");

        // An active workflow can't be edited into the slot another active one already holds:
        // there would be two live workflows competing for exactly the same applicants.
        if (existing.IsActive)
        {
            var newSignature = ApprovalScopeMatching.Signature(updated);
            var actives = await Context.ApprovalWorkflows.Include(w => w.Scopes)
                .Where(w => w.Id != workflowId && w.ApplicationType == existing.ApplicationType && w.IsActive)
                .ToListAsync(token);
            if (actives.Any(w => ApprovalScopeMatching.Signature(w) == newSignature))
                throw new GuardException(
                    "Another active workflow already applies to exactly these conditions. " +
                    "Change the conditions, or save this as a new version and activate that instead.");
        }

        existing.Name = updated.Name;
        existing.Priority = updated.Priority;
        existing.ScopeDepartmentId = updated.ScopeDepartmentId;
        existing.ReplacesWorkflowId = null;
        Context.ApprovalWorkflowSteps.RemoveRange(existing.Steps);
        Context.ApprovalWorkflowScopes.RemoveRange(existing.Scopes);
        await Context.SaveChangesAsync(token);
        existing.Steps.Clear();
        existing.Scopes.Clear();
        foreach (var step in updated.Steps)
        {
            existing.Steps.Add(step);
        }
        foreach (var scope in updated.Scopes)
        {
            existing.Scopes.Add(scope);
        }
        await CommitChangesAsync(token);
        return existing;
    }

    // Activate now: this workflow takes every new application of its type/scope from here on;
    // requests already running on the previous one finish there.
    public async Task ActivateAsync(Guid workflowId, CancellationToken token = default)
    {
        var workflow = await Context.ApprovalWorkflows.FirstOrDefaultAsync(w => w.Id == workflowId, token)
            ?? throw new NotFoundException("Workflow not found.");
        await ApprovalWorkflowSwitch.ActivateAsync(Context, workflow, token);
        await CommitChangesAsync(token);
    }

    public async Task DeactivateAsync(Guid workflowId, CancellationToken token = default)
    {
        var workflow = await Context.ApprovalWorkflows.FirstOrDefaultAsync(w => w.Id == workflowId, token)
            ?? throw new NotFoundException("Workflow not found.");
        workflow.IsActive = false;
        // Nothing left to take over from.
        await ApprovalWorkflowSwitch.ClearSchedulesReplacingAsync(Context, workflowId, token);
        await CommitChangesAsync(token);
    }

    /// <summary>
    /// Activate when the current one drains: the (inactive) version waits until the workflow now
    /// active for its type/scope has no requests in progress, then takes over. New applications
    /// keep using the current one until then. With nothing active, or nothing in progress, it
    /// takes over right away. Returns whether it's already active.
    /// </summary>
    public async Task<bool> ScheduleActivationAsync(Guid workflowId, CancellationToken token = default)
    {
        var workflow = await Context.ApprovalWorkflows.FirstOrDefaultAsync(w => w.Id == workflowId, token)
            ?? throw new NotFoundException("Workflow not found.");
        if (workflow.IsActive) throw new GuardException("This workflow is already active.");

        var current = (await ApprovalWorkflowSwitch.FindSlotHoldersAsync(Context, workflow, token)).FirstOrDefault();

        if (current == null)
        {
            await ApprovalWorkflowSwitch.ActivateAsync(Context, workflow, token);
        }
        else
        {
            // One waiting version per active workflow: the latest schedule wins.
            await ApprovalWorkflowSwitch.ClearSchedulesReplacingAsync(Context, current.Id, token);
            workflow.ReplacesWorkflowId = current.Id;
            // Flush (inside the unit of work's transaction) so the switch's lookups see the schedule.
            await Context.SaveChangesAsync(token);
            await ApprovalWorkflowSwitch.TryActivateReplacementAsync(Context, current.Id, excludingInstanceId: null, token);
        }
        await CommitChangesAsync(token);
        return workflow.IsActive;
    }

    public async Task CancelScheduledActivationAsync(Guid workflowId, CancellationToken token = default)
    {
        var workflow = await Context.ApprovalWorkflows.FirstOrDefaultAsync(w => w.Id == workflowId, token)
            ?? throw new NotFoundException("Workflow not found.");
        workflow.ReplacesWorkflowId = null;
        await CommitChangesAsync(token);
    }

    /// <summary>
    /// Keeps the screen honest: closes requests whose application is no longer pending (withdrawn,
    /// deleted -- older versions of the app left those "in progress" forever), then lets any
    /// scheduled version take over whose current workflow has now drained. Run when the list loads.
    /// </summary>
    public async Task ReconcileAsync(ApprovalApplicationType type, CancellationToken token = default)
    {
        var changed = await CloseStaleInstancesAsync(type, token) > 0;
        // Flush (inside the unit of work's transaction) so the drain check counts them as closed.
        if (changed) await Context.SaveChangesAsync(token);

        var waiting = await Context.ApprovalWorkflows
            .Where(w => w.ApplicationType == type && w.ReplacesWorkflowId != null && !w.IsActive)
            .Select(w => w.ReplacesWorkflowId!.Value)
            .ToListAsync(token);
        foreach (var currentId in waiting)
            changed |= await ApprovalWorkflowSwitch.TryActivateReplacementAsync(Context, currentId, excludingInstanceId: null, token);

        if (changed) await CommitChangesAsync(token);
    }

    // In-progress requests of an employee application type whose application isn't pending any
    // more. Other types (DTR/payroll batches) manage their own lifecycle and are left alone.
    private async Task<int> CloseStaleInstancesAsync(ApprovalApplicationType type, CancellationToken token)
    {
        var pendingIds = PendingApplicationIds(type);
        if (pendingIds == null) return 0;

        var stale = await Context.IgnoringConfidentiality<ApprovalInstance>()
            .Where(i => i.ApplicationType == type &&
                        i.Status == ApprovalInstanceStatus.InProgress &&
                        !pendingIds.Contains(i.ApplicationId))
            .ToListAsync(token);
        foreach (var instance in stale)
        {
            instance.Status = ApprovalInstanceStatus.Cancelled;
            instance.ReassignedApproverEmployeeId = null;
        }
        return stale.Count;
    }

    private IQueryable<Guid>? PendingApplicationIds(ApprovalApplicationType type) => type switch
    {
        ApprovalApplicationType.Leave => Context.IgnoringConfidentiality<LeaveApplication>()
            .Where(a => a.ApprovalStatus == ApprovalStatus.ForApproval).Select(a => a.Id),
        ApprovalApplicationType.Overtime => Context.IgnoringConfidentiality<OverTimeApplication>()
            .Where(a => a.ApprovalStatus == ApprovalStatus.ForApproval).Select(a => a.Id),
        ApprovalApplicationType.OfficialBusiness => Context.IgnoringConfidentiality<TravelOrderApplication>()
            .Where(a => a.ApprovalStatus == ApprovalStatus.ForApproval).Select(a => a.Id),
        ApprovalApplicationType.PassSlip => Context.IgnoringConfidentiality<PassSlipApplication>()
            .Where(a => a.ApprovalStatus == ApprovalStatus.ForApproval).Select(a => a.Id),
        ApprovalApplicationType.Loan => Context.IgnoringConfidentiality<DeductionApplication>()
            .Where(a => a.ApprovalStatus == ApprovalStatus.ForApproval).Select(a => a.Id),
        ApprovalApplicationType.ProfileUpdate => Context.IgnoringConfidentiality<Hrms.Domain.Entities.EmployeeEntities.EmployeeProfileUpdateRequest>()
            .Where(a => a.ApprovalStatus == ApprovalStatus.ForApproval).Select(a => a.Id),
        _ => null,
    };

    // A workflow can't change while any request is still running on it -- those read its steps
    // live. Once they've all finished it can; or copy it as a new version.
    public async Task EnsureEditableAsync(Guid workflowId, CancellationToken token = default)
    {
        var inProgress = await CountInProgressAsync(workflowId, token);
        if (inProgress > 0)
            throw new GuardException(
                $"{inProgress} {(inProgress == 1 ? "request is" : "requests are")} still going through this workflow. " +
                "Wait for them to finish, or copy it as a new version.");
    }

    public async Task DeleteAsync(Guid workflowId, CancellationToken token = default)
    {
        if (await HasInstancesAsync(workflowId, token))
            throw new GuardException(
                "This workflow has been used by past requests, so it can't be deleted. Deactivate it instead.");
        var workflow = await GetOneAsync(workflowId, token) ?? throw new NotFoundException("Workflow not found.");
        await RemoveAsync(workflow);
        await CommitChangesAsync(token);
    }

    /// <summary>
    /// Which active workflow an employee's applications of this type would run on right now, and
    /// the other active ones that were considered -- for the Setup screen's "which workflow
    /// applies?" check. Null winner = none matches, so the single-step fallback applies.
    /// </summary>
    public async Task<WorkflowResolution> ResolveForEmployeeAsync(
        ApprovalApplicationType type, Guid employeeId, CancellationToken token = default)
    {
        var employee = await Context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, token)
            ?? throw new NotFoundException("Employee not found.");
        var scope = ApprovalScopeContext.FromEmployee(employee);
        var active = await Context.ApprovalWorkflows.Include(w => w.Scopes)
            .Where(w => w.ApplicationType == type && w.IsActive)
            .AsNoTracking().ToListAsync(token);
        var winner = ApprovalScopeMatching.Select(active, scope);
        var candidates = ApprovalScopeMatching.Rank(active)
            .Select(w => new WorkflowCandidate(w, ApprovalScopeMatching.Matches(w, scope)))
            .ToList();
        return new WorkflowResolution(winner, candidates);
    }

    /// <summary>
    /// For each active workflow, the other active ones that could apply to the same applicants and
    /// share its priority -- which of them wins for such an applicant is decided only by how
    /// specific they are (then age), which the admin probably didn't intend.
    /// </summary>
    public static Dictionary<Guid, List<string>> FindSamePriorityOverlaps(IReadOnlyCollection<ApprovalWorkflow> workflows)
    {
        var active = workflows.Where(w => w.IsActive).ToList();
        return active.ToDictionary(
            w => w.Id,
            w => active
                .Where(o => o.Id != w.Id && o.Priority == w.Priority && ApprovalScopeMatching.Overlaps(w, o))
                .Select(o => o.Name)
                .ToList());
    }

    private const int MaxGroupsPerStep = 5;

    // Extra pools on a step: each is validated like a step's own approver (it must name who, a level in
    // range, a fallback person when that is the policy, skip only on a per-applicant type) and needs a
    // quorum of at least one. Unlike a step, a pool may skip on the first step -- the step's own approver
    // is still there.
    private static void ValidateGroups(ApprovalWorkflowStep step)
    {
        if (step.Groups.Count == 0) return;
        if (step.Groups.Count > MaxGroupsPerStep)
            throw new InvalidOperationException($"Step {step.StepNumber}: at most {MaxGroupsPerStep} extra approver groups.");

        foreach (var group in step.Groups)
        {
            var label = $"Step {step.StepNumber}, group {group.GroupNumber}";
            if (group.MinApprovals < 1)
                throw new InvalidOperationException($"{label}: it needs at least 1 approval.");

            var named = group.ApproverType switch
            {
                ApproverType.Person => group.ApproverEmployeeId.HasValue,
                ApproverType.Department => group.ApproverDepartmentId.HasValue,
                ApproverType.Position => group.ApproverPositionId.HasValue,
                ApproverType.ApplicantDepartment => true,
                _ => ApprovalApproverResolver.IsDynamic(group.ApproverType)
                    && (group.ApproverLevel is null or (>= 1 and <= ApprovalApproverResolver.MaxLevel)),
            };
            if (!named)
                throw new InvalidOperationException($"{label}: an approver must be selected for type {group.ApproverType}.");
            if (group.OnUnresolved == UnresolvedApproverPolicy.FallbackPerson && group.FallbackEmployeeId is null)
                throw new InvalidOperationException($"{label}: choose the person to use when no approver is found.");
            if (group.OnUnresolved == UnresolvedApproverPolicy.Skip && !ApprovalApproverResolver.IsDynamic(group.ApproverType))
                throw new InvalidOperationException($"{label}: only groups that look up the approver per applicant can be skipped.");
        }
    }

    // Reminder and escalation timing: positive whole hours (at most 90 days), and an escalation needs a
    // person to escalate to. A repeat only means something after a first reminder.
    private const int MaxSlaHours = 24 * 90;

    private static void ValidateSla(ApprovalWorkflowStep step)
    {
        static bool Bad(int? hours) => hours is not null && (hours < 1 || hours > MaxSlaHours);
        if (Bad(step.RemindAfterHours) || Bad(step.RemindRepeatHours) || Bad(step.EscalateAfterHours))
            throw new InvalidOperationException($"Step {step.StepNumber}: reminder and escalation times must be 1 to {MaxSlaHours} hours.");
        if (step.RemindRepeatHours.HasValue && !step.RemindAfterHours.HasValue)
            throw new InvalidOperationException($"Step {step.StepNumber}: set when the first reminder goes out before repeating it.");
        if (step.EscalateAfterHours.HasValue && step.EscalateToEmployeeId is null)
            throw new InvalidOperationException($"Step {step.StepNumber}: choose the person to escalate to.");
        if (step.EscalateToEmployeeId.HasValue && !step.EscalateAfterHours.HasValue)
            throw new InvalidOperationException($"Step {step.StepNumber}: set how long to wait before escalating.");
    }

    // A step that applies only above/below a threshold: the measure must exist for the request type,
    // it needs a value, and it can't be step 1 (a skipped first step could finish the request without
    // the module's own status update).
    private static void ValidateCondition(ApprovalWorkflowStep step, ApprovalApplicationType type)
    {
        if (step.ConditionMetric == ApprovalStepConditionMetric.None) return;

        if (!ApprovalStepCondition.IsOfferedFor(step.ConditionMetric, type))
            throw new InvalidOperationException(
                $"Step {step.StepNumber}: {step.ConditionMetric} isn't something a {type} request has, so a step can't depend on it.");
        if (step.ConditionValue is null or < 0)
            throw new InvalidOperationException($"Step {step.StepNumber}: enter the amount the condition compares with.");
        if (step.StepNumber == 1)
            throw new InvalidOperationException("The first step can't be conditional; it has to apply to every request.");
    }

    private const int MaxPriority = 9999;

    private static void ValidateScope(ApprovalWorkflow workflow)
    {
        if (workflow.Priority is < 1 or > MaxPriority)
            throw new InvalidOperationException($"Priority must be between 1 and {MaxPriority}.");

        var seen = new HashSet<(ApprovalScopeDimension, string)>();
        foreach (var scope in workflow.Scopes)
        {
            var value = ApprovalScopeMatching.ValueOf(scope);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"A {scope.Dimension} condition needs a value.");
            if (scope.Dimension == ApprovalScopeDimension.EmploymentStatus)
            {
                if (!Enum.TryParse<EmploymentStatus>(scope.TargetValue, out _))
                    throw new InvalidOperationException($"'{scope.TargetValue}' is not an employment status.");
            }
            else if (scope.TargetId == null)
            {
                throw new InvalidOperationException($"A {scope.Dimension} condition needs a selection.");
            }
            // The same value twice would only trip the unique index.
            if (!seen.Add((scope.Dimension, value)))
                throw new InvalidOperationException($"{scope.Dimension} has a duplicate value.");
        }
    }

    private static void ValidateSteps(IEnumerable<ApprovalWorkflowStep> steps, ApprovalApplicationType type)
    {
        foreach (var step in steps)
        {
            ValidateCondition(step, type);
            ValidateSla(step);
            ValidateGroups(step);
            var isValid = step.ApproverType switch
            {
                ApproverType.Person => step.ApproverEmployeeId.HasValue,
                ApproverType.Department => step.ApproverDepartmentId.HasValue,
                ApproverType.Position => step.ApproverPositionId.HasValue,
                ApproverType.ApplicantManager => true,
                ApproverType.ApplicantDepartment => true,
                ApproverType.ApplicantManagerLevel or ApproverType.ApplicantDepartmentHead
                    => step.ApproverLevel is null or (>= 1 and <= ApprovalApproverResolver.MaxLevel),
                ApproverType.ApplicantSectionHead or ApproverType.ApplicantBranchHead
                    or ApproverType.ApplicantAreaHead or ApproverType.ApplicantClientHead => true,
                _ => false,
            };
            if (!isValid)
                throw new InvalidOperationException(
                    $"Step {step.StepNumber}: an approver must be selected for type {step.ApproverType}" +
                    (step.ApproverLevel.HasValue ? $" (the level must be 1 to {ApprovalApproverResolver.MaxLevel})." : "."));

            // What to do when a dynamic step finds nobody.
            if (step.OnUnresolved == UnresolvedApproverPolicy.FallbackPerson && step.FallbackEmployeeId is null)
                throw new InvalidOperationException($"Step {step.StepNumber}: choose the person to use when no approver is found.");
            if (step.OnUnresolved == UnresolvedApproverPolicy.Skip)
            {
                if (!ApprovalApproverResolver.IsDynamic(step.ApproverType))
                    throw new InvalidOperationException($"Step {step.StepNumber}: only steps that look up the approver per applicant can be skipped.");
                // A skipped first step could finish the request without the module's own status update.
                if (step.StepNumber == 1)
                    throw new InvalidOperationException("The first step can't be skipped when no approver is found; choose a fallback person or leave it waiting.");
            }
        }
    }
}
