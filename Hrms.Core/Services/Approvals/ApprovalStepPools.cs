using Hrms.Domain.Entities.Approvals;

namespace Hrms.Core.Services.Approvals;

/// <summary>
/// One pool of approvers a step needs a quorum from. A step's own approver is pool one (GroupId null);
/// each extra group on the step is another. The Effective step is what the rest of the engine
/// already understands -- the step itself, or a step built from the group's fields -- so eligibility,
/// resolving a dynamic approver and finding who to notify work for a pool exactly as for a single step.
/// </summary>
public sealed record ApprovalStepPool(Guid? GroupId, ApprovalWorkflowStep Effective, int MinApprovals);

/// <summary>How far one pool is: approvals so far, how many it needs, and whether it was dropped.</summary>
public sealed record PoolProgress(ApprovalStepPool Pool, int Approved, bool Dropped)
{
    public int Required => Pool.MinApprovals;
    public bool Satisfied => Dropped || Approved >= Required;
}

public static class ApprovalStepPools
{
    /// <summary>
    /// The group id recorded on an approval that decided for the whole step -- an Owner/Admin override, a
    /// reassigned approver, or the person a late step was escalated to -- so it counts for every pool.
    /// </summary>
    public static readonly Guid WholeStep = Guid.Empty;

    /// <summary>The step's pools: its own approver first, then its extra groups in order.</summary>
    public static IReadOnlyList<ApprovalStepPool> Of(ApprovalWorkflowStep step)
    {
        var pools = new List<ApprovalStepPool> { new(null, step, Math.Max(1, step.MinApprovals)) };
        foreach (var group in step.Groups.OrderBy(g => g.GroupNumber))
        {
            pools.Add(new ApprovalStepPool(group.Id, new ApprovalWorkflowStep
            {
                StepNumber = step.StepNumber,
                ApproverType = group.ApproverType,
                ApproverEmployeeId = group.ApproverEmployeeId,
                ApproverDepartmentId = group.ApproverDepartmentId,
                ApproverDepartment = group.ApproverDepartment,
                ApproverPositionId = group.ApproverPositionId,
                ApproverPosition = group.ApproverPosition,
                ApproverLevel = group.ApproverLevel,
                OnUnresolved = group.OnUnresolved,
                FallbackEmployeeId = group.FallbackEmployeeId,
                MinApprovals = group.MinApprovals,
                NamedApprovers = [],
            }, Math.Max(1, group.MinApprovals)));
        }
        return pools;
    }

    /// <summary>A step with extra groups needs several pools to agree, which changes how approvals count.</summary>
    public static bool IsParallel(ApprovalWorkflowStep step) => step.Groups.Count > 0;

    /// <summary>
    /// Approvals each pool has so far this cycle. An approval counts for the pool it was recorded
    /// against; one that decided for the whole step (<see cref="WholeStep"/>) counts for all of them. A
    /// person counts once however they acted (themself or for a delegator).
    /// </summary>
    public static List<PoolProgress> Progress(
        IReadOnlyList<ApprovalStepPool> pools, IEnumerable<ApprovalAction> actionsOfThisStep, ISet<Guid>? droppedGroupIds = null)
    {
        var approvals = actionsOfThisStep.Where(a => a.Action == ApprovalActionType.Approved).ToList();
        var wholeStep = approvals.Any(a => a.GroupId == WholeStep);
        return pools.Select(pool =>
        {
            var dropped = droppedGroupIds != null && droppedGroupIds.Contains(pool.GroupId ?? Guid.Empty);
            var approved = wholeStep
                ? pool.MinApprovals
                : approvals.Where(a => a.GroupId == pool.GroupId)
                    .Select(a => a.OnBehalfOfEmployeeId ?? a.ActorEmployeeId).OfType<Guid>().Distinct().Count();
            return new PoolProgress(pool, approved, dropped);
        }).ToList();
    }

    /// <summary>Every pool that still counts has its quorum.</summary>
    public static bool IsCleared(IReadOnlyList<PoolProgress> progress) => progress.All(p => p.Satisfied);
}
