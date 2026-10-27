using Hrms.Domain.Entities.Approvals;

namespace Hrms.Core.Services.Approvals;

/// <summary>
/// Who a step is waiting on, in words: a person's name, a department or position, or a dynamic
/// approver ("Your Branch Head: Ana Reyes"). <paramref name="subject"/> is whose manager/head the
/// dynamic roles are -- null reads "Your ..." (the applicant's own timeline), "Applicant" reads
/// "Applicant's ..." (an approver's list).
/// </summary>
public static class ApprovalStepLabeler
{
    /// <summary>A step's label: its approver, or -- with several pools -- all of them joined ("HR + Finance").</summary>
    public static string? StepLabel(
        ApprovalWorkflowStep? step, IReadOnlyDictionary<Guid, string> names,
        IReadOnlyDictionary<(int Step, Guid? Group), ResolvedApprovers> resolvedByPool, string? subject = null)
    {
        if (step == null) return null;
        if (!ApprovalStepPools.IsParallel(step))
            return PoolLabel(step, names, resolvedByPool.GetValueOrDefault((step.StepNumber, null)), subject);
        var parts = ApprovalStepPools.Of(step)
            .Select(p => PoolLabel(p.Effective, names, resolvedByPool.GetValueOrDefault((step.StepNumber, p.GroupId)), subject))
            .Where(label => label != null);
        return string.Join(" + ", parts);
    }

    /// <summary>One pool's approver (a step's own approver, or one of its groups).</summary>
    public static string? PoolLabel(
        ApprovalWorkflowStep? step, IReadOnlyDictionary<Guid, string> names, ResolvedApprovers? resolved, string? subject = null)
    {
        // The plain "applicant's manager" step keeps its long-standing label.
        if (step != null && resolved != null && step.ApproverType != ApproverType.ApplicantManager)
            return ApprovalStepLabels.Dynamic(
                step, resolved.EmployeeIds.Select(id => names.GetValueOrDefault(id)).FirstOrDefault(n => n != null), resolved.Unresolved, subject);
        return FixedLabel(step, names, subject);
    }

    private static string? FixedLabel(ApprovalWorkflowStep? step, IReadOnlyDictionary<Guid, string> names, string? subject) => step?.ApproverType switch
    {
        ApproverType.Person => step.ApproverEmployeeId is { } approverId
            ? names.GetValueOrDefault(approverId)
            : null,
        ApproverType.Department => step.ApproverDepartment?.Name,
        ApproverType.Position => step.ApproverPosition?.Name,
        ApproverType.ApplicantManager => $"{ApprovalStepLabels.Possessive(subject)} Manager",
        ApproverType.ApplicantDepartment => $"{ApprovalStepLabels.Possessive(subject)} Department",
        _ => null,
    };
}
