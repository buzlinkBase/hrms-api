using Hrms.Domain;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Approvals;

// Everything IApproverEligibilityStrategy needs to decide whether Caller may act on the current
// step, with no DB access of its own -- callers assemble this from already-loaded entities so
// the strategies stay pure and trivially unit-testable.
public record ApprovalEligibilityContext(
    Employee Caller,
    Employee Applicant,
    ApprovalWorkflowStep? Step, // null = implicit legacy fallback step (no workflow configured)
    IReadOnlyCollection<Guid> NamedApproverEmployeeIds,
    // Set when an Owner/Admin has reassigned the CURRENT step to a specific employee (see
    // ApprovalInstance.ReassignedApproverEmployeeId) -- when present, fully replaces the
    // Step's own ApproverType resolution for eligibility purposes.
    Guid? ReassignedApproverEmployeeId = null,
    // Employees who have delegated their approvals to the Caller right now (for this type): the
    // Caller may act wherever one of them could have. Null/empty = no delegation.
    IReadOnlyCollection<Employee>? DelegatorsOfCaller = null,
    // Who a dynamic step (manager level, department/section/branch/site/client head) resolved to
    // for this applicant -- worked out beforehand (ApprovalApproverResolver) so these strategies
    // stay free of I/O. Null = not resolved, the step's own strategy decides.
    IReadOnlyCollection<Guid>? ResolvedApproverEmployeeIds = null,
    // Set when the current step waited past its escalation time: this person may act as well as the
    // step's own approvers (see ApprovalInstance.EscalatedToEmployeeId). Unlike a reassignment it adds
    // to them.
    Guid? EscalatedToEmployeeId = null);

/// <summary>Whether the caller may act, and -- when only via a delegation -- for whom.</summary>
public sealed record ApprovalStanding(
    bool Eligible,
    Guid? OnBehalfOfEmployeeId,
    bool IsEscalated = false,
    // A step with several pools of approvers: the pool the caller's approval counts for (null = the
    // step's own approver), or -- ClearsWholeStep -- that they decide for every pool (a reassigned
    // approver, an escalation). See ApprovalStepPools.
    Guid? PoolId = null,
    bool ClearsWholeStep = false)
{
    public static readonly ApprovalStanding None = new(false, null);
    public static readonly ApprovalStanding Direct = new(true, null);
    public static readonly ApprovalStanding Escalated = new(true, null, true);
    public bool IsDelegated => OnBehalfOfEmployeeId.HasValue;

    /// <summary>
    /// Standing the workflow itself grants, so the type's Approve permission isn't also required: acting
    /// for a delegator, or as the person a late step escalated to.
    /// </summary>
    public bool GrantsAccess => IsDelegated || IsEscalated;
}

// One resolution rule per ApproverType (Enums.ApproverType) -- see the approver-type table in
// the approved plan. Kept as a Strategy per type rather than one branching method so each rule
// stays independently readable and testable.
public interface IApproverEligibilityStrategy
{
    ApproverType ApplicableTo { get; }
    bool IsEligible(ApprovalEligibilityContext context);
}

public class PersonApproverStrategy : IApproverEligibilityStrategy
{
    public ApproverType ApplicableTo => ApproverType.Person;

    public bool IsEligible(ApprovalEligibilityContext context) =>
        context.Step?.ApproverEmployeeId is { } approverId && context.Caller.Id == approverId;
}

public class DepartmentApproverStrategy : IApproverEligibilityStrategy
{
    public ApproverType ApplicableTo => ApproverType.Department;

    public bool IsEligible(ApprovalEligibilityContext context)
    {
        if (context.Caller.UserId is null) return false;
        if (context.Step?.ApproverDepartmentId is not { } departmentId) return false;
        if (context.Caller.DepartmentId != departmentId) return false;

        return context.NamedApproverEmployeeIds.Count == 0
            || context.NamedApproverEmployeeIds.Contains(context.Caller.Id);
    }
}

public class PositionApproverStrategy : IApproverEligibilityStrategy
{
    public ApproverType ApplicableTo => ApproverType.Position;

    public bool IsEligible(ApprovalEligibilityContext context)
    {
        if (context.Caller.UserId is null) return false;
        if (context.Step?.ApproverPositionId is not { } positionId) return false;
        if (context.Caller.PositionId != positionId) return false;

        return context.NamedApproverEmployeeIds.Count == 0
            || context.NamedApproverEmployeeIds.Contains(context.Caller.Id);
    }
}

// The dynamic types worked out beforehand: eligible exactly when the caller is among the people
// the step resolved to for this applicant.
public class ResolvedApproverStrategy : IApproverEligibilityStrategy
{
    public ResolvedApproverStrategy(ApproverType applicableTo) => ApplicableTo = applicableTo;

    public ApproverType ApplicableTo { get; }

    public bool IsEligible(ApprovalEligibilityContext context) =>
        context.ResolvedApproverEmployeeIds?.Contains(context.Caller.Id) == true;
}

// Dynamic -- resolved fresh against whichever applicant filed THIS application, not a fixed
// employee picked when the workflow was configured. May sit at any step number.
public class ApplicantManagerApproverStrategy : IApproverEligibilityStrategy
{
    public ApproverType ApplicableTo => ApproverType.ApplicantManager;

    public bool IsEligible(ApprovalEligibilityContext context) =>
        context.Applicant.ManagerId is { } managerId && context.Caller.Id == managerId;
}

// Dynamic -- resolved fresh against whichever department the applicant themself belongs to, not
// a department fixed when the workflow was configured. May sit at any step number.
public class ApplicantDepartmentApproverStrategy : IApproverEligibilityStrategy
{
    public ApproverType ApplicableTo => ApproverType.ApplicantDepartment;

    public bool IsEligible(ApprovalEligibilityContext context)
    {
        if (context.Caller.UserId is null) return false;
        if (context.Applicant.DepartmentId is not { } departmentId) return false;
        if (context.Caller.DepartmentId != departmentId) return false;

        return context.NamedApproverEmployeeIds.Count == 0
            || context.NamedApproverEmployeeIds.Contains(context.Caller.Id);
    }
}

// Dispatches to the strategy matching the current step's ApproverType. The implicit legacy
// fallback step (Step == null, no workflow configured for this tenant/type) is eligible to
// everyone here by design -- that case is gated entirely by the caller's coarse
// {Row}:Approve permission at the controller layer, exactly like today.
public class ApproverEligibilityResolver
{
    private static readonly IReadOnlyDictionary<ApproverType, IApproverEligibilityStrategy> Strategies =
        new IApproverEligibilityStrategy[]
        {
            new PersonApproverStrategy(),
            new DepartmentApproverStrategy(),
            new PositionApproverStrategy(),
            new ApplicantManagerApproverStrategy(),
            new ApplicantDepartmentApproverStrategy(),
            new ResolvedApproverStrategy(ApproverType.ApplicantManagerLevel),
            new ResolvedApproverStrategy(ApproverType.ApplicantDepartmentHead),
            new ResolvedApproverStrategy(ApproverType.ApplicantSectionHead),
            new ResolvedApproverStrategy(ApproverType.ApplicantBranchHead),
            new ResolvedApproverStrategy(ApproverType.ApplicantAreaHead),
            new ResolvedApproverStrategy(ApproverType.ApplicantClientHead),
        }.ToDictionary(s => s.ApplicableTo);

    // "Applicant's manager" keeps its own rule, unless the step was resolved (it can have a
    // fallback person when the applicant has no manager recorded).
    private static readonly ResolvedApproverStrategy ResolvedManager = new(ApproverType.ApplicantManager);

    public bool IsEligible(ApprovalEligibilityContext context) => Resolve(context).Eligible;

    // Direct eligibility first (the caller acts as themself); otherwise the first delegator the
    // step would have accepted. A reassigned step stays exclusive to the person it was reassigned
    // to -- nobody, delegates included, acts in their place.
    public ApprovalStanding Resolve(ApprovalEligibilityContext context)
    {
        // The person a late step was escalated to may act, whoever else is eligible.
        if (context.EscalatedToEmployeeId is { } escalatedId && context.Caller.Id == escalatedId)
            return ApprovalStanding.Escalated;

        if (context.ReassignedApproverEmployeeId is { } reassignedId)
            return context.Caller.Id == reassignedId ? ApprovalStanding.Direct : ApprovalStanding.None;

        if (context.Step is null) return ApprovalStanding.Direct;

        if (!Strategies.TryGetValue(context.Step.ApproverType, out var strategy)) return ApprovalStanding.None;
        if (context.Step.ApproverType == ApproverType.ApplicantManager && context.ResolvedApproverEmployeeIds != null)
            strategy = ResolvedManager;
        if (strategy.IsEligible(context)) return ApprovalStanding.Direct;

        foreach (var delegator in context.DelegatorsOfCaller ?? [])
        {
            if (delegator.Id == context.Caller.Id) continue;
            if (strategy.IsEligible(context with { Caller = delegator, DelegatorsOfCaller = null }))
                return new ApprovalStanding(true, delegator.Id);
        }

        return ApprovalStanding.None;
    }
}

// Quorum counting for Department/Position/ApplicantDepartment steps -- a step clears once at
// least MinApprovals *distinct* employees have approved it. Person and ApplicantManager always
// resolve to one person, so MinApprovals is moot for them (Math.Max floors it at 1 regardless).
public static class ApprovalQuorum
{
    public static bool IsStepCleared(int minApprovals, IEnumerable<Guid> approverIdsForStep) =>
        approverIdsForStep.Distinct().Count() >= Math.Max(1, minApprovals);
}
