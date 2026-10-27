using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Approvals;

/// <summary>
/// What a workflow's scope conditions are matched against: the applicant's org attributes, or --
/// for DTR/payroll batches, whose "applicant" is just whoever generated the batch -- only the
/// batch's own payroll group. A dimension the context doesn't carry never matches a condition.
/// </summary>
public sealed record ApprovalScopeContext(
    Guid? EmployeeId,
    Guid? BranchId,
    Guid? DepartmentId,
    Guid? ClientId,
    Guid? AreaId,
    Guid? PayrollGroupId,
    Guid? PositionId,
    Guid? SectionId,
    EmploymentStatus? EmploymentStatus,
    // Leave requests: the leave type filed (from the request's attributes).
    Guid? LeaveTypeId = null)
{
    public static ApprovalScopeContext FromEmployee(Employee employee, ApprovalRequestAttributes? attributes = null) => new(
        employee.Id, employee.BranchId, employee.DepartmentId, employee.ClientId, employee.AreaId,
        employee.PayrollGroupId, employee.PositionId, employee.SectionId, employee.EmploymentStatus,
        attributes?.LeaveTypeId);

    /// <summary>For batch approvals (DTR, payroll): only the batch's payroll group is known.</summary>
    public static ApprovalScopeContext ForPayrollGroup(Guid? payrollGroupId) =>
        new(null, null, null, null, null, payrollGroupId, null, null, null);

    /// <summary>
    /// For a payroll batch built from rows: its payroll group when they all share one, else none
    /// known (a run spanning several groups only matches workflows with no payroll-group condition).
    /// </summary>
    public static ApprovalScopeContext ForPayrollGroups(IEnumerable<Guid?> payrollGroupIds)
    {
        var distinct = payrollGroupIds.Where(g => g.HasValue).Distinct().Take(2).ToList();
        return ForPayrollGroup(distinct.Count == 1 ? distinct[0] : null);
    }
}

/// <summary>
/// Decides which active workflow of a type an application runs on. Pure functions -- no I/O -- so
/// the rules are testable in isolation.
///
/// A workflow's conditions are its scope rows (plus the legacy single department). It matches
/// when, for every dimension it has conditions on, the context's value is one of that dimension's
/// values (AND across dimensions, OR within one). No conditions = the company-wide default, which
/// matches everyone. Among matches the lowest Priority wins, then the most dimensions (most
/// specific), then the oldest.
/// </summary>
public static class ApprovalScopeMatching
{
    // One reader per dimension: the context's value as the same string a condition stores.
    private static readonly IReadOnlyDictionary<ApprovalScopeDimension, Func<ApprovalScopeContext, string?>> Readers =
        new Dictionary<ApprovalScopeDimension, Func<ApprovalScopeContext, string?>>
        {
            [ApprovalScopeDimension.Branch] = c => Key(c.BranchId),
            [ApprovalScopeDimension.Department] = c => Key(c.DepartmentId),
            [ApprovalScopeDimension.Client] = c => Key(c.ClientId),
            [ApprovalScopeDimension.Area] = c => Key(c.AreaId),
            [ApprovalScopeDimension.PayrollGroup] = c => Key(c.PayrollGroupId),
            [ApprovalScopeDimension.Position] = c => Key(c.PositionId),
            [ApprovalScopeDimension.Section] = c => Key(c.SectionId),
            [ApprovalScopeDimension.EmploymentStatus] = c => c.EmploymentStatus?.ToString(),
            [ApprovalScopeDimension.Employee] = c => Key(c.EmployeeId),
            [ApprovalScopeDimension.LeaveType] = c => Key(c.LeaveTypeId),
        };

    private static string? Key(Guid? id) => id?.ToString();

    /// <summary>The stored value of one scope row, in the same form the readers produce.</summary>
    public static string? ValueOf(ApprovalWorkflowScope scope) => scope.TargetId?.ToString() ?? scope.TargetValue;

    /// <summary>Every dimension the workflow restricts, with its allowed values.</summary>
    public static IReadOnlyDictionary<ApprovalScopeDimension, HashSet<string>> ConditionsOf(ApprovalWorkflow workflow)
    {
        var conditions = new Dictionary<ApprovalScopeDimension, HashSet<string>>();
        foreach (var scope in workflow.Scopes)
        {
            if (ValueOf(scope) is not { } value) continue;
            if (!conditions.TryGetValue(scope.Dimension, out var values))
                conditions[scope.Dimension] = values = [];
            values.Add(value);
        }

        // A workflow saved before scope rows existed: its single department counts as a condition.
        if (workflow.ScopeDepartmentId is { } legacy)
        {
            if (!conditions.TryGetValue(ApprovalScopeDimension.Department, out var departments))
                conditions[ApprovalScopeDimension.Department] = departments = [];
            departments.Add(legacy.ToString());
        }

        return conditions;
    }

    public static bool Matches(ApprovalWorkflow workflow, ApprovalScopeContext context) =>
        ConditionsOf(workflow).All(c => Readers[c.Key](context) is { } value && c.Value.Contains(value));

    /// <summary>How many dimensions the workflow restricts -- more means more specific.</summary>
    public static int Specificity(ApprovalWorkflow workflow) => ConditionsOf(workflow).Count;

    /// <summary>
    /// Canonical text of the workflow's conditions; two workflows with the same signature compete
    /// for the same slot (one active at a time -- the newer version replaces the older). Empty for
    /// the company-wide default.
    /// </summary>
    public static string Signature(ApprovalWorkflow workflow) => string.Join('|',
        ConditionsOf(workflow)
            .OrderBy(c => c.Key.ToString(), StringComparer.Ordinal)
            .Select(c => $"{c.Key}:{string.Join(',', c.Value.OrderBy(v => v, StringComparer.Ordinal))}"));

    /// <summary>
    /// Whether some applicant could match both workflows: for every dimension both restrict, they
    /// share at least one value (a dimension only one of them restricts never rules anyone out).
    /// </summary>
    public static bool Overlaps(ApprovalWorkflow a, ApprovalWorkflow b)
    {
        var left = ConditionsOf(a);
        var right = ConditionsOf(b);
        return left.All(c => !right.TryGetValue(c.Key, out var other) || c.Value.Overlaps(other));
    }

    /// <summary>The active workflow the context runs on, or null when none matches (the fallback).</summary>
    public static ApprovalWorkflow? Select(IEnumerable<ApprovalWorkflow> activeWorkflows, ApprovalScopeContext context) =>
        Rank(activeWorkflows.Where(w => Matches(w, context))).FirstOrDefault();

    /// <summary>Winner first: lowest priority, then most specific, then oldest.</summary>
    public static IEnumerable<ApprovalWorkflow> Rank(IEnumerable<ApprovalWorkflow> workflows) => workflows
        .OrderBy(w => w.Priority)
        .ThenByDescending(Specificity)
        .ThenBy(w => w.CreatedAt)
        .ThenBy(w => w.Id);
}
