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
            .Include(w => w.ScopeDepartment)
            .Where(w => w.ApplicationType == type)
            .OrderByDescending(w => w.IsActive).ThenBy(w => w.Name)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(token);

    public async Task<ApprovalWorkflow?> GetAsync(Guid id, CancellationToken token = default) =>
        await Context.ApprovalWorkflows
            .Include(w => w.Steps.OrderBy(s => s.StepNumber)).ThenInclude(s => s.NamedApprovers)
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
        ValidateSteps(workflow.Steps);
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
        ValidateSteps(updated.Steps);

        var existing = await Context.ApprovalWorkflows
            .Include(w => w.Steps)
            .ThenInclude(s => s.NamedApprovers)
            .FirstOrDefaultAsync(w => w.Id == workflowId, token)
            ?? throw new NotFoundException("Workflow not found.");

        existing.Name = updated.Name;
        existing.ScopeDepartmentId = updated.ScopeDepartmentId;
        existing.ReplacesWorkflowId = null;
        Context.ApprovalWorkflowSteps.RemoveRange(existing.Steps);
        await Context.SaveChangesAsync(token);
        existing.Steps.Clear();
        foreach (var step in updated.Steps)
        {
            existing.Steps.Add(step);
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

        var current = await Context.ApprovalWorkflows.FirstOrDefaultAsync(w =>
            w.Id != workflowId &&
            w.ApplicationType == workflow.ApplicationType &&
            w.ScopeDepartmentId == workflow.ScopeDepartmentId &&
            w.IsActive, token);

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

    private static void ValidateSteps(IEnumerable<ApprovalWorkflowStep> steps)
    {
        foreach (var step in steps)
        {
            var isValid = step.ApproverType switch
            {
                ApproverType.Person => step.ApproverEmployeeId.HasValue,
                ApproverType.Department => step.ApproverDepartmentId.HasValue,
                ApproverType.Position => step.ApproverPositionId.HasValue,
                ApproverType.ApplicantManager => true,
                ApproverType.ApplicantDepartment => true,
                _ => false,
            };
            if (!isValid)
                throw new InvalidOperationException(
                    $"Step {step.StepNumber}: an approver must be selected for type {step.ApproverType}.");
        }
    }
}
