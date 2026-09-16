using System.Linq.Expressions;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// ApprovalEngineService — the I/O orchestration around ApproverEligibilityResolver/
/// ApprovalQuorum (see ApprovalEligibilityTests for those rules in isolation). These tests exercise
/// StartAsync's workflow resolution and RecordActionAsync's full advance/quorum/decline/override
/// behavior end-to-end through the same IRepository mocking every other service test in this
/// project uses -- the engine deliberately never touches raw Context, specifically so this works.
/// </summary>
public class ApprovalEngineServiceTests
{
    private static Employee BuildEmployee(Guid? id = null, Guid? userId = null, Guid? departmentId = null, Guid? managerId = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        UserId = userId,
        DepartmentId = departmentId,
        ManagerId = managerId,
        EmployeeNo = "EMP-001",
        Skills = [], Educations = [], Dependents = [], EmployeeRecords = [], Employments = [], Assets = [], RestDays = [],
    };

    private static (ApprovalEngineService Service, List<ApprovalInstance> Instances, List<ApprovalAction> Actions) BuildService(
        List<Employee> employees, List<ApprovalWorkflow> workflows)
    {
        var instances = new List<ApprovalInstance>();
        var actions = new List<ApprovalAction>();

        var repo = Substitute.For<IRepository>();
        repo.Find<Employee>(Arg.Any<Expression<Func<Employee, bool>>>())
            .Returns(call => employees.Where(call.Arg<Expression<Func<Employee, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<ApprovalWorkflow>(Arg.Any<Expression<Func<ApprovalWorkflow, bool>>>())
            .Returns(call => workflows.Where(call.Arg<Expression<Func<ApprovalWorkflow, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<ApprovalInstance>(Arg.Any<Expression<Func<ApprovalInstance, bool>>>())
            .Returns(call => instances.Where(call.Arg<Expression<Func<ApprovalInstance, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.AddAsync(Arg.Any<ApprovalInstance>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask())
            .AndDoes(call => instances.Add(call.Arg<ApprovalInstance>()));
        repo.AddAsync(Arg.Any<ApprovalAction>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask())
            .AndDoes(call =>
            {
                var action = call.Arg<ApprovalAction>();
                actions.Add(action);
                instances.Single(i => i.Id == action.ApprovalInstanceId).Actions.Add(action);
            });

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        return (new ApprovalEngineService(uow), instances, actions);
    }

    [Fact]
    public async Task StartAsync_NoConfiguredWorkflow_CreatesImplicitFallbackInstance()
    {
        var applicant = BuildEmployee();
        var (service, instances, _) = BuildService([applicant], []);

        var instance = await service.StartAsync(ApprovalApplicationType.Leave, Guid.NewGuid(), applicant.Id, CancellationToken.None);

        instance.ApprovalWorkflowId.Should().BeNull();
        instance.Status.Should().Be(ApprovalInstanceStatus.InProgress);
        instance.CurrentStepNumber.Should().Be(1);
        instances.Should().ContainSingle();
    }

    [Fact]
    public async Task StartAsync_DepartmentScopedWorkflow_WinsOverTenantWideDefault()
    {
        var departmentId = Guid.NewGuid();
        var applicant = BuildEmployee(departmentId: departmentId);
        var tenantWide = new ApprovalWorkflow { Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, IsActive = true, ScopeDepartmentId = null, Steps = [] };
        var scoped = new ApprovalWorkflow { Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, IsActive = true, ScopeDepartmentId = departmentId, Steps = [] };
        var (service, _, _) = BuildService([applicant], [tenantWide, scoped]);

        var instance = await service.StartAsync(ApprovalApplicationType.Leave, Guid.NewGuid(), applicant.Id, CancellationToken.None);

        instance.ApprovalWorkflowId.Should().Be(scoped.Id);
    }

    [Fact]
    public async Task RecordActionAsync_SingleStepPersonWorkflow_ApprovesImmediately()
    {
        var applicant = BuildEmployee();
        var approver = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps =
            [
                new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NamedApprovers = [] },
            ],
        };
        var (service, _, _) = BuildService([applicant, approver], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        var result = await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        result.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task RecordActionAsync_IneligibleCaller_Throws()
    {
        var applicant = BuildEmployee();
        var approver = BuildEmployee();
        var stranger = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        var (service, _, _) = BuildService([applicant, approver, stranger], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        var act = () => service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, stranger.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RecordActionAsync_OverrideAccess_BypassesEligibility()
    {
        var applicant = BuildEmployee();
        var approver = BuildEmployee();
        var admin = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        var (service, _, _) = BuildService([applicant, approver, admin], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        var result = await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, admin.Id,
            callerHasOverrideAccess: true, ApprovalActionType.Approved, note: null, CancellationToken.None);

        result.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task RecordActionAsync_Decline_ShortCircuitsRegardlessOfRemainingSteps()
    {
        var applicant = BuildEmployee();
        var step1Approver = BuildEmployee();
        var step2Approver = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps =
            [
                new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = step1Approver.Id, MinApprovals = 1, NamedApprovers = [] },
                new ApprovalWorkflowStep { StepNumber = 2, ApproverType = ApproverType.Person, ApproverEmployeeId = step2Approver.Id, MinApprovals = 1, NamedApprovers = [] },
            ],
        };
        var (service, _, _) = BuildService([applicant, step1Approver, step2Approver], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        var result = await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, step1Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Declined, note: null, CancellationToken.None);

        result.InstanceStatus.Should().Be(ApprovalInstanceStatus.Declined);
    }

    [Fact]
    public async Task RecordActionAsync_MultiStepWorkflow_AdvancesOnlyAfterEachStepClears()
    {
        var applicant = BuildEmployee();
        var step1Approver = BuildEmployee();
        var step2Approver = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps =
            [
                new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = step1Approver.Id, MinApprovals = 1, NamedApprovers = [] },
                new ApprovalWorkflowStep { StepNumber = 2, ApproverType = ApproverType.Person, ApproverEmployeeId = step2Approver.Id, MinApprovals = 1, NamedApprovers = [] },
            ],
        };
        var (service, _, _) = BuildService([applicant, step1Approver, step2Approver], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        var afterStep1 = await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, step1Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        afterStep1.InstanceStatus.Should().Be(ApprovalInstanceStatus.InProgress);
        afterStep1.CurrentStepNumber.Should().Be(2);

        // Step 1's approver has no standing on step 2.
        var wrongApprover = () => service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, step1Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        await wrongApprover.Should().ThrowAsync<UnauthorizedAccessException>();

        var afterStep2 = await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, step2Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        afterStep2.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task RecordActionAsync_DepartmentQuorumOfTwo_ClearsOnlyOnSecondDistinctApprover()
    {
        var applicant = BuildEmployee();
        var departmentId = Guid.NewGuid();
        var approver1 = BuildEmployee(userId: Guid.NewGuid(), departmentId: departmentId);
        var approver2 = BuildEmployee(userId: Guid.NewGuid(), departmentId: departmentId);
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Department, ApproverDepartmentId = departmentId, MinApprovals = 2, NamedApprovers = [] }],
        };
        var (service, _, _) = BuildService([applicant, approver1, approver2], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        var afterFirst = await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver1.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        afterFirst.InstanceStatus.Should().Be(ApprovalInstanceStatus.InProgress);

        var afterSecond = await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver2.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        afterSecond.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task RecordActionAsync_RequiredNoteMissing_Throws()
    {
        var applicant = BuildEmployee();
        var approver = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NoteRequirement = NoteRequirement.Required, NamedApprovers = [] }],
        };
        var (service, _, _) = BuildService([applicant, approver], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        var act = () => service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: "   ", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RecordActionAsync_NoPriorStartAsyncCall_SelfHealsImplicitInstance()
    {
        // Simulates an application created before this engine shipped -- no StartAsync was ever
        // called for it, so there's no ApprovalInstance row. RecordActionAsync must still work,
        // exactly like today's single-step behavior, instead of throwing NotFound.
        var applicant = BuildEmployee();
        var approver = BuildEmployee();
        var (service, _, _) = BuildService([applicant, approver], []);

        var result = await service.RecordActionAsync(
            ApprovalApplicationType.Overtime, Guid.NewGuid(), applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        result.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }
}
