using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;

namespace hrms.test.ServiceTests;

/// <summary>
/// ApproverEligibilityResolver / ApprovalQuorum — the pure decision logic behind the
/// configurable multi-level approval engine. Unlike most services in this project these need no
/// IRepository/IUnitOfWorkService mocking at all: ApprovalEngineService loads everything ahead
/// of time and hands these plain in-memory objects, so the rules themselves are trivially
/// testable in isolation. See ApprovalEngineService for the I/O wrapper around them.
/// </summary>
public class ApprovalEligibilityTests
{
    private static Employee BuildEmployee(Guid? id = null, Guid? userId = null, Guid? departmentId = null, Guid? positionId = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        UserId = userId,
        DepartmentId = departmentId,
        PositionId = positionId,
        EmployeeNo = "EMP-001",
        Skills = [],
        Educations = [],
        Dependents = [],
        EmployeeRecords = [],
        Employments = [],
        Assets = [],
        RestDays = [],
    };

    private static ApprovalWorkflowStep BuildStep(
        ApproverType type,
        Guid? approverEmployeeId = null,
        Guid? approverDepartmentId = null,
        Guid? approverPositionId = null,
        int minApprovals = 1) => new()
    {
        StepNumber = 1,
        ApproverType = type,
        ApproverEmployeeId = approverEmployeeId,
        ApproverDepartmentId = approverDepartmentId,
        ApproverPositionId = approverPositionId,
        MinApprovals = minApprovals,
        NamedApprovers = [],
    };

    private readonly ApproverEligibilityResolver _resolver = new();

    [Fact]
    public void NoConfiguredWorkflow_AnyCallerIsEligible()
    {
        var caller = BuildEmployee();
        var applicant = BuildEmployee();

        var eligible = _resolver.IsEligible(new ApprovalEligibilityContext(caller, applicant, Step: null, []));

        eligible.Should().BeTrue();
    }

    [Fact]
    public void Person_ExactMatchRequired()
    {
        var approver = BuildEmployee();
        var stranger = BuildEmployee();
        var applicant = BuildEmployee();
        var step = BuildStep(ApproverType.Person, approverEmployeeId: approver.Id);

        _resolver.IsEligible(new ApprovalEligibilityContext(approver, applicant, step, [])).Should().BeTrue();
        _resolver.IsEligible(new ApprovalEligibilityContext(stranger, applicant, step, [])).Should().BeFalse();
    }

    [Fact]
    public void Department_AnyoneInDepartmentWithUserId_IsEligible()
    {
        var departmentId = Guid.NewGuid();
        var member = BuildEmployee(userId: Guid.NewGuid(), departmentId: departmentId);
        var outsider = BuildEmployee(userId: Guid.NewGuid(), departmentId: Guid.NewGuid());
        var applicant = BuildEmployee();
        var step = BuildStep(ApproverType.Department, approverDepartmentId: departmentId);

        _resolver.IsEligible(new ApprovalEligibilityContext(member, applicant, step, [])).Should().BeTrue();
        _resolver.IsEligible(new ApprovalEligibilityContext(outsider, applicant, step, [])).Should().BeFalse();
    }

    [Fact]
    public void Department_MemberWithoutUserId_IsNotEligible()
    {
        var departmentId = Guid.NewGuid();
        var memberNoLogin = BuildEmployee(userId: null, departmentId: departmentId);
        var applicant = BuildEmployee();
        var step = BuildStep(ApproverType.Department, approverDepartmentId: departmentId);

        _resolver.IsEligible(new ApprovalEligibilityContext(memberNoLogin, applicant, step, [])).Should().BeFalse();
    }

    [Fact]
    public void Department_NarrowedToNamedApprovers_ExcludesOtherMembers()
    {
        var departmentId = Guid.NewGuid();
        var named = BuildEmployee(userId: Guid.NewGuid(), departmentId: departmentId);
        var unnamed = BuildEmployee(userId: Guid.NewGuid(), departmentId: departmentId);
        var applicant = BuildEmployee();
        var step = BuildStep(ApproverType.Department, approverDepartmentId: departmentId);

        var context = new ApprovalEligibilityContext(named, applicant, step, [named.Id]);
        var otherContext = new ApprovalEligibilityContext(unnamed, applicant, step, [named.Id]);

        _resolver.IsEligible(context).Should().BeTrue();
        _resolver.IsEligible(otherContext).Should().BeFalse();
    }

    [Fact]
    public void Position_WhoeverCurrentlyHoldsIt_IsEligible()
    {
        var positionId = Guid.NewGuid();
        var holder = BuildEmployee(userId: Guid.NewGuid(), positionId: positionId);
        var nonHolder = BuildEmployee(userId: Guid.NewGuid(), positionId: Guid.NewGuid());
        var applicant = BuildEmployee();
        var step = BuildStep(ApproverType.Position, approverPositionId: positionId);

        _resolver.IsEligible(new ApprovalEligibilityContext(holder, applicant, step, [])).Should().BeTrue();
        _resolver.IsEligible(new ApprovalEligibilityContext(nonHolder, applicant, step, [])).Should().BeFalse();
    }

    [Fact]
    public void ApplicantManager_ResolvesPerApplicant_NotAFixedPerson()
    {
        var manager = BuildEmployee();
        var otherEmployee = BuildEmployee();
        var applicant = BuildEmployee();
        applicant.ManagerId = manager.Id;
        var step = BuildStep(ApproverType.ApplicantManager);

        _resolver.IsEligible(new ApprovalEligibilityContext(manager, applicant, step, [])).Should().BeTrue();
        _resolver.IsEligible(new ApprovalEligibilityContext(otherEmployee, applicant, step, [])).Should().BeFalse();
    }

    [Fact]
    public void ApplicantDepartment_ResolvesAgainstApplicantsOwnDepartment()
    {
        var departmentId = Guid.NewGuid();
        var applicant = BuildEmployee(departmentId: departmentId);
        var sameDeptCaller = BuildEmployee(userId: Guid.NewGuid(), departmentId: departmentId);
        var otherDeptCaller = BuildEmployee(userId: Guid.NewGuid(), departmentId: Guid.NewGuid());
        var step = BuildStep(ApproverType.ApplicantDepartment);

        _resolver.IsEligible(new ApprovalEligibilityContext(sameDeptCaller, applicant, step, [])).Should().BeTrue();
        _resolver.IsEligible(new ApprovalEligibilityContext(otherDeptCaller, applicant, step, [])).Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, false)]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, true)]
    public void Quorum_ClearsOnlyOnceDistinctApproversReachMinimum(int minApprovals, int distinctApprovers, bool expectCleared)
    {
        var approverIds = Enumerable.Range(0, distinctApprovers).Select(_ => Guid.NewGuid()).ToArray();

        ApprovalQuorum.IsStepCleared(minApprovals, approverIds).Should().Be(expectCleared);
    }

    [Fact]
    public void Quorum_DuplicateApproverDoesNotCountTwice()
    {
        var sameApproverTwice = new[] { Guid.NewGuid() };
        var repeated = sameApproverTwice.Concat(sameApproverTwice);

        ApprovalQuorum.IsStepCleared(2, repeated).Should().BeFalse();
    }
}
