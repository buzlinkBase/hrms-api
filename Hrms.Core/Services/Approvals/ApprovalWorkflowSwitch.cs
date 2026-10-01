using Hrms.Domain.Entities.Approvals;

namespace Hrms.Core.Services.Approvals;

/// <summary>
/// The rules for when a workflow may change and when a scheduled version takes over. Shared by
/// ApprovalWorkflowService (the Setup screen, which commits) and ApprovalEngineService (which
/// never commits -- its callers own the save), so it only changes tracked entities.
///
/// Counts deliberately ignore the confidential-employee filter: a request from someone the
/// caller can't see still runs on the workflow and must keep it from changing under it.
/// </summary>
public static class ApprovalWorkflowSwitch
{
    /// <summary>Requests still going through the workflow, leaving out one being closed right now.</summary>
    public static Task<int> CountInProgressAsync(
        HrmsContext context, Guid workflowId, Guid? excludingInstanceId, CancellationToken token) =>
        context.IgnoringConfidentiality<ApprovalInstance>()
            .CountAsync(i => i.ApprovalWorkflowId == workflowId &&
                             i.Status == ApprovalInstanceStatus.InProgress &&
                             i.Id != excludingInstanceId, token);

    /// <summary>
    /// Makes <paramref name="workflow"/> the active one for its application type and scope: any
    /// other active one there is switched off, and schedules waiting on those are dropped (what
    /// they were waiting to replace is gone).
    /// </summary>
    public static async Task ActivateAsync(HrmsContext context, ApprovalWorkflow workflow, CancellationToken token)
    {
        var holders = await context.ApprovalWorkflows
            .Where(w => w.Id != workflow.Id &&
                        w.ApplicationType == workflow.ApplicationType &&
                        w.ScopeDepartmentId == workflow.ScopeDepartmentId &&
                        w.IsActive)
            .ToListAsync(token);
        foreach (var holder in holders)
        {
            holder.IsActive = false;
            await ClearSchedulesReplacingAsync(context, holder.Id, token);
        }

        workflow.IsActive = true;
        workflow.ReplacesWorkflowId = null;
    }

    /// <summary>Drops every schedule waiting to replace <paramref name="workflowId"/>.</summary>
    public static async Task ClearSchedulesReplacingAsync(HrmsContext context, Guid workflowId, CancellationToken token)
    {
        var waiting = await context.ApprovalWorkflows
            .Where(w => w.ReplacesWorkflowId == workflowId)
            .ToListAsync(token);
        foreach (var w in waiting) w.ReplacesWorkflowId = null;
    }

    /// <summary>
    /// If a version is scheduled to replace <paramref name="currentWorkflowId"/> and nothing is
    /// in progress on it any more, switches to that version. Returns whether it did.
    /// </summary>
    public static async Task<bool> TryActivateReplacementAsync(
        HrmsContext context, Guid currentWorkflowId, Guid? excludingInstanceId, CancellationToken token)
    {
        var replacement = await context.ApprovalWorkflows
            .FirstOrDefaultAsync(w => w.ReplacesWorkflowId == currentWorkflowId && !w.IsActive, token);
        if (replacement == null) return false;
        if (await CountInProgressAsync(context, currentWorkflowId, excludingInstanceId, token) > 0) return false;

        await ActivateAsync(context, replacement, token);
        Log.Information("Approval workflow {Replacement} took over from {Current} after its last pending request",
            replacement.Id, currentWorkflowId);
        return true;
    }
}
