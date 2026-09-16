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
    IReadOnlyCollection<Guid> NamedApproverEmployeeIds);

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
        }.ToDictionary(s => s.ApplicableTo);

    public bool IsEligible(ApprovalEligibilityContext context)
    {
        if (context.Step is null) return true;

        return Strategies.TryGetValue(context.Step.ApproverType, out var strategy)
            && strategy.IsEligible(context);
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
