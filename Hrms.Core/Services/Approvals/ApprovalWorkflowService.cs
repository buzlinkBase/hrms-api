using Hrms.Domain.Entities.Approvals;

namespace Hrms.Core.Services.Approvals;

// CRUD for the saved policy (ApprovalWorkflow/ApprovalWorkflowStep/ApprovalWorkflowStepApprover)
// that the Setup > Approval Workflows screen manages. ApprovalEngineService is the separate
// runtime piece that actually walks an application through whatever this service has saved.
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
        await Context.ApprovalInstances.AnyAsync(i => i.ApprovalWorkflowId == workflowId, token);

    public async Task<ApprovalWorkflow> CreateWorkflowAsync(ApprovalWorkflow workflow, CancellationToken token = default)
    {
        ValidateSteps(workflow.Steps);
        await CreateAsync(workflow, token);
        await CommitChangesAsync(token);
        return workflow;
    }

    // Replaces an unused (no instances yet) workflow's Name/Scope/Steps wholesale -- the Setup
    // screen's Edit action. Once any instance exists this refuses via EnsureEditableAsync, same
    // as Delete; change the chain by creating and activating a new version instead.
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

    // Flips this workflow active for its (ApplicationType, ScopeDepartmentId) slot and
    // deactivates whatever previously held that slot -- the literal "assign which approval flow
    // will be used" action from the approved plan. A workflow with existing instances can still
    // be activated (that's just re-enabling it going forward); it just can no longer have its
    // steps edited -- see EnsureEditableAsync.
    public async Task ActivateAsync(Guid workflowId, CancellationToken token = default)
    {
        var workflow = await GetOneAsync(workflowId, token) ?? throw new NotFoundException("Workflow not found.");

        var currentHolder = await Context.ApprovalWorkflows.FirstOrDefaultAsync(w =>
            w.Id != workflowId &&
            w.ApplicationType == workflow.ApplicationType &&
            w.ScopeDepartmentId == workflow.ScopeDepartmentId &&
            w.IsActive, token);
        if (currentHolder != null) currentHolder.IsActive = false;

        workflow.IsActive = true;
        await CommitChangesAsync(token);
    }

    public async Task DeactivateAsync(Guid workflowId, CancellationToken token = default)
    {
        var workflow = await GetOneAsync(workflowId, token) ?? throw new NotFoundException("Workflow not found.");
        workflow.IsActive = false;
        await CommitChangesAsync(token);
    }

    // Workflows are immutable once used -- once any ApprovalInstance references one, its steps
    // can no longer change so in-flight requests keep the shape they started with. Changing the
    // chain means creating a new workflow version and activating it instead.
    public async Task EnsureEditableAsync(Guid workflowId, CancellationToken token = default)
    {
        if (await HasInstancesAsync(workflowId, token))
            throw new InvalidOperationException(
                "This workflow has already been used and can no longer be edited. Deactivate it and create a new version instead.");
    }

    public async Task DeleteAsync(Guid workflowId, CancellationToken token = default)
    {
        await EnsureEditableAsync(workflowId, token);
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
