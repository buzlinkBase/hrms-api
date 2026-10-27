using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities.Approvals;

namespace hrms.test.ServiceTests;

/// <summary>
/// Which active workflow an application runs on when several are active for the type: every
/// condition dimension must match (AND), values within a dimension are alternatives (OR), no
/// conditions = the company-wide default, and ties go to the lowest priority, then the most
/// specific, then the oldest.
/// </summary>
public class ApprovalScopeMatchingTests
{
    private static readonly Guid BranchA = Guid.NewGuid();
    private static readonly Guid BranchB = Guid.NewGuid();
    private static readonly Guid DeptHr = Guid.NewGuid();
    private static readonly Guid DeptIt = Guid.NewGuid();
    private static readonly Guid SiteOne = Guid.NewGuid();
    private static readonly Guid GroupSm = Guid.NewGuid();
    private static readonly Guid Ana = Guid.NewGuid();

    private static ApprovalWorkflow Flow(string name, int priority = 100, params ApprovalWorkflowScope[] scopes) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Priority = priority,
        IsActive = true,
        ApplicationType = ApprovalApplicationType.Leave,
        CreatedAt = new DateTime(2026, 1, 1),
        Scopes = scopes.ToList(),
    };

    private static ApprovalWorkflowScope Cond(ApprovalScopeDimension dimension, Guid id) =>
        new() { Dimension = dimension, TargetId = id };

    private static ApprovalWorkflowScope Status(EmploymentStatus status) =>
        new() { Dimension = ApprovalScopeDimension.EmploymentStatus, TargetValue = status.ToString() };

    private static ApprovalScopeContext Employee(Guid? branch = null, Guid? dept = null, Guid? area = null,
        Guid? group = null, EmploymentStatus? status = null, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), branch, dept, null, area, group, null, null, status);

    [Fact]
    public void NoConditions_IsTheDefault_AndMatchesEveryone()
    {
        var all = Flow("Default");

        ApprovalScopeMatching.Matches(all, Employee()).Should().BeTrue();
        ApprovalScopeMatching.Matches(all, Employee(branch: BranchA, dept: DeptHr)).Should().BeTrue();
        ApprovalScopeMatching.Specificity(all).Should().Be(0);
        ApprovalScopeMatching.Signature(all).Should().BeEmpty();
    }

    [Fact]
    public void ValuesWithinADimension_AreAlternatives()
    {
        var either = Flow("A or B", 100, Cond(ApprovalScopeDimension.Branch, BranchA), Cond(ApprovalScopeDimension.Branch, BranchB));

        ApprovalScopeMatching.Matches(either, Employee(branch: BranchA)).Should().BeTrue();
        ApprovalScopeMatching.Matches(either, Employee(branch: BranchB)).Should().BeTrue();
        ApprovalScopeMatching.Matches(either, Employee(branch: Guid.NewGuid())).Should().BeFalse();
        ApprovalScopeMatching.Specificity(either).Should().Be(1);
    }

    [Fact]
    public void DifferentDimensions_AreAllRequired()
    {
        var both = Flow("A in HR", 100, Cond(ApprovalScopeDimension.Branch, BranchA), Cond(ApprovalScopeDimension.Department, DeptHr));

        ApprovalScopeMatching.Matches(both, Employee(branch: BranchA, dept: DeptHr)).Should().BeTrue();
        ApprovalScopeMatching.Matches(both, Employee(branch: BranchA, dept: DeptIt)).Should().BeFalse();
        ApprovalScopeMatching.Matches(both, Employee(branch: BranchB, dept: DeptHr)).Should().BeFalse();
        ApprovalScopeMatching.Specificity(both).Should().Be(2);
    }

    [Fact]
    public void AMissingAttribute_NeverMatchesACondition()
    {
        var byBranch = Flow("A", 100, Cond(ApprovalScopeDimension.Branch, BranchA));

        ApprovalScopeMatching.Matches(byBranch, Employee(branch: null)).Should().BeFalse();
    }

    [Fact]
    public void EveryDimension_ReadsItsOwnAttribute()
    {
        ApprovalScopeMatching.Matches(Flow("site", 100, Cond(ApprovalScopeDimension.Area, SiteOne)), Employee(area: SiteOne)).Should().BeTrue();
        ApprovalScopeMatching.Matches(Flow("group", 100, Cond(ApprovalScopeDimension.PayrollGroup, GroupSm)), Employee(group: GroupSm)).Should().BeTrue();
        ApprovalScopeMatching.Matches(Flow("who", 100, Cond(ApprovalScopeDimension.Employee, Ana)), Employee(id: Ana)).Should().BeTrue();
        ApprovalScopeMatching.Matches(Flow("who", 100, Cond(ApprovalScopeDimension.Employee, Ana)), Employee()).Should().BeFalse();
        ApprovalScopeMatching.Matches(Flow("status", 100, Status(EmploymentStatus.Regular)), Employee(status: EmploymentStatus.Regular)).Should().BeTrue();
        ApprovalScopeMatching.Matches(Flow("status", 100, Status(EmploymentStatus.Regular)), Employee(status: EmploymentStatus.Contract)).Should().BeFalse();
    }

    [Fact]
    public void ALegacyDepartmentScope_CountsAsADepartmentCondition()
    {
        var legacy = Flow("HR only");
        legacy.ScopeDepartmentId = DeptHr;

        ApprovalScopeMatching.Matches(legacy, Employee(dept: DeptHr)).Should().BeTrue();
        ApprovalScopeMatching.Matches(legacy, Employee(dept: DeptIt)).Should().BeFalse();
        ApprovalScopeMatching.Specificity(legacy).Should().Be(1);
    }

    [Fact]
    public void LowestPriorityWins_EvenOverAMoreSpecificWorkflow()
    {
        var specific = Flow("Branch A in HR", 50, Cond(ApprovalScopeDimension.Branch, BranchA), Cond(ApprovalScopeDimension.Department, DeptHr));
        var urgent = Flow("Department HR", 10, Cond(ApprovalScopeDimension.Department, DeptHr));

        ApprovalScopeMatching.Select([specific, urgent], Employee(branch: BranchA, dept: DeptHr))
            .Should().BeSameAs(urgent);
    }

    [Fact]
    public void WithEqualPriority_TheMoreSpecificWins()
    {
        var general = Flow("Branch A", 100, Cond(ApprovalScopeDimension.Branch, BranchA));
        var specific = Flow("Branch A in HR", 100, Cond(ApprovalScopeDimension.Branch, BranchA), Cond(ApprovalScopeDimension.Department, DeptHr));
        var fallback = Flow("Default");

        ApprovalScopeMatching.Select([fallback, general, specific], Employee(branch: BranchA, dept: DeptHr)).Should().BeSameAs(specific);
        ApprovalScopeMatching.Select([fallback, general, specific], Employee(branch: BranchA, dept: DeptIt)).Should().BeSameAs(general);
        ApprovalScopeMatching.Select([fallback, general, specific], Employee(branch: BranchB)).Should().BeSameAs(fallback);
    }

    [Fact]
    public void WithEqualPriorityAndSpecificity_TheOldestWins()
    {
        var newer = Flow("newer", 100, Cond(ApprovalScopeDimension.Branch, BranchA));
        newer.CreatedAt = new DateTime(2026, 6, 1);
        var older = Flow("older", 100, Cond(ApprovalScopeDimension.Department, DeptHr));

        ApprovalScopeMatching.Select([newer, older], Employee(branch: BranchA, dept: DeptHr)).Should().BeSameAs(older);
    }

    [Fact]
    public void NothingMatching_SelectsNull_SoTheSingleStepFallbackApplies()
    {
        var byBranch = Flow("A", 100, Cond(ApprovalScopeDimension.Branch, BranchA));

        ApprovalScopeMatching.Select([byBranch], Employee(branch: BranchB)).Should().BeNull();
        ApprovalScopeMatching.Select([], Employee()).Should().BeNull();
    }

    [Fact]
    public void ABatchContext_OnlyMatchesPayrollGroupOrDefaultWorkflows()
    {
        var batch = ApprovalScopeContext.ForPayrollGroup(GroupSm);
        var byGroup = Flow("SM batches", 100, Cond(ApprovalScopeDimension.PayrollGroup, GroupSm));
        var byBranch = Flow("Branch A", 100, Cond(ApprovalScopeDimension.Branch, BranchA));
        var fallback = Flow("Default");

        ApprovalScopeMatching.Select([byBranch, byGroup, fallback], batch).Should().BeSameAs(byGroup);
        ApprovalScopeMatching.Select([byBranch, fallback], batch).Should().BeSameAs(fallback);
    }

    [Fact]
    public void ARunAcrossSeveralPayrollGroups_HasNoKnownGroup()
    {
        ApprovalScopeContext.ForPayrollGroups([GroupSm, GroupSm]).PayrollGroupId.Should().Be(GroupSm);
        ApprovalScopeContext.ForPayrollGroups([GroupSm, Guid.NewGuid()]).PayrollGroupId.Should().BeNull();
        ApprovalScopeContext.ForPayrollGroups([null, GroupSm]).PayrollGroupId.Should().Be(GroupSm);
        ApprovalScopeContext.ForPayrollGroups([]).PayrollGroupId.Should().BeNull();
    }

    [Fact]
    public void Signature_IsOrderIndependent_AndDistinguishesConditions()
    {
        var one = Flow("one", 100, Cond(ApprovalScopeDimension.Branch, BranchA), Cond(ApprovalScopeDimension.Branch, BranchB), Cond(ApprovalScopeDimension.Department, DeptHr));
        var same = Flow("same", 5, Cond(ApprovalScopeDimension.Department, DeptHr), Cond(ApprovalScopeDimension.Branch, BranchB), Cond(ApprovalScopeDimension.Branch, BranchA));
        var other = Flow("other", 100, Cond(ApprovalScopeDimension.Branch, BranchA), Cond(ApprovalScopeDimension.Department, DeptHr));

        ApprovalScopeMatching.Signature(one).Should().Be(ApprovalScopeMatching.Signature(same));
        ApprovalScopeMatching.Signature(one).Should().NotBe(ApprovalScopeMatching.Signature(other));
    }

    [Fact]
    public void Overlaps_WhenSomeoneCouldMatchBoth()
    {
        var branchA = Flow("A", 100, Cond(ApprovalScopeDimension.Branch, BranchA));
        var hr = Flow("HR", 100, Cond(ApprovalScopeDimension.Department, DeptHr));
        var branchB = Flow("B", 100, Cond(ApprovalScopeDimension.Branch, BranchB));
        var aInHr = Flow("A in HR", 100, Cond(ApprovalScopeDimension.Branch, BranchA), Cond(ApprovalScopeDimension.Department, DeptHr));
        var everyone = Flow("Default");

        ApprovalScopeMatching.Overlaps(branchA, hr).Should().BeTrue();       // an HR employee in branch A
        ApprovalScopeMatching.Overlaps(branchA, branchB).Should().BeFalse(); // nobody is in both branches
        ApprovalScopeMatching.Overlaps(branchA, aInHr).Should().BeTrue();
        ApprovalScopeMatching.Overlaps(branchB, aInHr).Should().BeFalse();
        ApprovalScopeMatching.Overlaps(everyone, branchA).Should().BeTrue();
    }
}
