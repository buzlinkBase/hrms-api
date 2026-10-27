using System.Linq.Expressions;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
using MockQueryable.NSubstitute;
using NSubstitute;
using Onepunch.AuditEngine;
using Onepunch.Common.Lib.DTO;

namespace hrms.test.ServiceTests;

/// <summary>
/// ApprovalEngineService — the I/O orchestration around ApproverEligibilityResolver/
/// ApprovalQuorum (see ApprovalEligibilityTests for those rules in isolation), plus the
/// ApprovalNotificationRequested events it publishes on step transitions/resolution. These tests
/// exercise StartAsync's workflow resolution and RecordActionAsync's full advance/quorum/
/// decline/override/notification behavior end-to-end through the same IRepository mocking every
/// other service test in this project uses -- the engine deliberately never touches raw Context,
/// specifically so this works.
/// </summary>
public class ApprovalEngineServiceTests
{
    private static Employee BuildEmployee(Guid? id = null, Guid? userId = null, Guid? departmentId = null, Guid? managerId = null, string? email = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        UserId = userId,
        DepartmentId = departmentId,
        ManagerId = managerId,
        Email = email,
        FirstName = "Test",
        LastName = "Employee",
        EmployeeNo = "EMP-001",
        Skills = [], Educations = [], Dependents = [], EmployeeRecords = [], Employments = [], Assets = [], RestDays = [],
    };

    private static (ApprovalEngineService Service, List<ApprovalInstance> Instances, List<ApprovalAction> Actions, IPublishEndpoint Publisher) BuildService(
        List<Employee> employees, List<ApprovalWorkflow> workflows, List<NotificationPreference>? preferences = null,
        IAuditWriter? audit = null)
    {
        var instances = new List<ApprovalInstance>();
        var actions = new List<ApprovalAction>();
        preferences ??= [];

        var repo = Substitute.For<IRepository>();
        repo.Find<Employee>(Arg.Any<Expression<Func<Employee, bool>>>())
            .Returns(call => employees.Where(call.Arg<Expression<Func<Employee, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<ApprovalWorkflow>(Arg.Any<Expression<Func<ApprovalWorkflow, bool>>>())
            .Returns(call => workflows.Where(call.Arg<Expression<Func<ApprovalWorkflow, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<ApprovalInstance>(Arg.Any<Expression<Func<ApprovalInstance, bool>>>())
            .Returns(call => instances.Where(call.Arg<Expression<Func<ApprovalInstance, bool>>>().Compile()).ToList().BuildMockDbSet());
        // Empty by default -- ResolveDeliveryFlagsAsync's opt-out default (both channels on) is
        // exactly what every test that doesn't seed a preference expects.
        repo.Find<NotificationPreference>(Arg.Any<Expression<Func<NotificationPreference, bool>>>())
            .Returns(call => preferences.Where(call.Arg<Expression<Func<NotificationPreference, bool>>>().Compile()).ToList().BuildMockDbSet());
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

        var publisher = Substitute.For<IPublishEndpoint>();
        return (new ApprovalEngineService(uow, publisher, audit), instances, actions, publisher);
    }

    // ---- audit trail: one Approve/Reject event per approve/decline click ----

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken token = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    // Stranger: a seeded employee who is not an approver for any step.
    private static readonly Employee Stranger = BuildEmployee();

    private static (ApprovalEngineService Service, RecordingAuditWriter Audit, Employee Applicant, Employee Approver) BuildAudited(
        int steps = 1, int minApprovals = 1)
    {
        var applicant = BuildEmployee();
        applicant.FirstName = "Ana";
        applicant.LastName = "Reyes";
        var approver = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = Enumerable.Range(1, steps)
                .Select(n => new ApprovalWorkflowStep
                {
                    StepNumber = n, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id,
                    MinApprovals = minApprovals, NamedApprovers = [], NoteRequirement = NoteRequirement.Optional,
                })
                .ToList(),
        };
        var audit = new RecordingAuditWriter();
        var (service, _, _, _) = BuildService([applicant, approver, Stranger], [workflow], audit: audit);
        return (service, audit, applicant, approver);
    }

    [Fact]
    public async Task Approving_RecordsOneApproveEvent_OnTheApplicationsOwnRecord()
    {
        var (service, audit, applicant, approver) = BuildAudited(steps: 2);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        await service.RecordActionAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        var approved = audit.Events.Should().ContainSingle().Subject;
        approved.EventType.Should().Be(AuditEventTypes.Approve);
        approved.EntityType.Should().Be("Leave", "the same entity type the LeaveApplication's own create/update events use");
        approved.EntityId.Should().Be(applicationId.ToString());
        approved.Description.Should().Be("Approved Leave of Ana Reyes (step 1 of 2)");
        approved.Metadata["Step"].Should().Be("1");
    }

    [Fact]
    public async Task Declining_RecordsARejectEvent_WithTheNote()
    {
        var (service, audit, applicant, approver) = BuildAudited();
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        await service.RecordActionAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Declined, note: "Peak season", CancellationToken.None);

        var rejected = audit.Events.Should().ContainSingle().Subject;
        rejected.EventType.Should().Be(AuditEventTypes.Reject);
        rejected.Description.Should().Be("Declined Leave of Ana Reyes");
        rejected.Metadata["Note"].Should().Be("Peak season");
    }

    [Fact]
    public async Task RefusedOrRepeatedActions_RecordNothingExtra()
    {
        // Two approvals needed, so the step stays open after the first click.
        var (service, audit, applicant, approver) = BuildAudited(minApprovals: 2);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        var ineligible = () => service.RecordActionAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, Stranger.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        await ineligible.Should().ThrowAsync<UnauthorizedAccessException>();
        audit.Events.Should().BeEmpty("a refused action is not an approval");

        // The same approver's click, then a double-click / client retry of it.
        for (var i = 0; i < 2; i++)
        {
            await service.RecordActionAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
                callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        }

        audit.Events.Should().ContainSingle("the repeat is ignored by the engine, so it is not audited either");
    }

    [Fact]
    public async Task StartAsync_NoConfiguredWorkflow_CreatesImplicitFallbackInstance()
    {
        var applicant = BuildEmployee();
        var (service, instances, _, _) = BuildService([applicant], []);

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
        var (service, _, _, _) = BuildService([applicant], [tenantWide, scoped]);

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
        var (service, _, _, _) = BuildService([applicant, approver], [workflow]);

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
        var (service, _, _, _) = BuildService([applicant, approver, stranger], [workflow]);

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
        var (service, _, _, _) = BuildService([applicant, approver, admin], [workflow]);

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
        var (service, _, _, _) = BuildService([applicant, step1Approver, step2Approver], [workflow]);

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
        var (service, _, _, _) = BuildService([applicant, step1Approver, step2Approver], [workflow]);

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
        var (service, _, _, _) = BuildService([applicant, approver1, approver2], [workflow]);

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
    public async Task RecordActionAsync_SameApproverActsTwiceOnSameStep_SecondCallIsANoOp()
    {
        // MinApprovals: 2 on a single-approver-capable step, so the FIRST call deliberately does
        // NOT clear quorum or advance CurrentStepNumber -- the second call genuinely lands on
        // the same still-current step, which is exactly what the new guard targets (a step that
        // resolves/advances on the first call is already covered by the pre-existing
        // InProgress-only check further up, since Status would no longer be InProgress by the
        // time a second call arrived).
        var applicant = BuildEmployee();
        var approver = BuildEmployee();
        var otherApprover = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 2, NamedApprovers = [] }],
        };
        var (service, _, actions, publisher) = BuildService([applicant, approver, otherApprover], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        var first = await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        first.InstanceStatus.Should().Be(ApprovalInstanceStatus.InProgress);
        first.CurrentStepNumber.Should().Be(1);

        // Double-click/retry of the exact same action on the exact same still-current step.
        var second = await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        second.Should().Be(first);
        actions.Should().ContainSingle(a => a.ActorEmployeeId == approver.Id);
        await publisher.DidNotReceive().Publish(Arg.Any<ApprovalNotificationRequested>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_DepartmentQuorumOfTwo_SameApproverDoubleClicking_DoesNotClearQuorumAlone()
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
        var (service, _, actions, _) = BuildService([applicant, approver1, approver2], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver1.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        // approver1 double-clicks -- must not count as a second, distinct approval.
        var afterDoubleClick = await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver1.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        afterDoubleClick.InstanceStatus.Should().Be(ApprovalInstanceStatus.InProgress);
        actions.Should().ContainSingle(a => a.ActorEmployeeId == approver1.Id);

        var afterDistinctSecond = await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver2.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        afterDistinctSecond.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
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
        var (service, _, _, _) = BuildService([applicant, approver], [workflow]);

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
        var (service, _, _, _) = BuildService([applicant, approver], []);

        var result = await service.RecordActionAsync(
            ApprovalApplicationType.Overtime, Guid.NewGuid(), applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        result.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task StartAsync_ConfiguredWorkflow_NotifiesFirstStepApprover()
    {
        var applicant = BuildEmployee();
        var approver = BuildEmployee(email: "approver@test.com");
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        var (service, _, _, publisher) = BuildService([applicant, approver], [workflow]);

        await service.StartAsync(ApprovalApplicationType.Leave, Guid.NewGuid(), applicant.Id, CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.RecipientEmail == "approver@test.com" && m.StatusLabel == "Pending Your Approval"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_NoConfiguredWorkflow_PublishesNoNotification()
    {
        var applicant = BuildEmployee();
        var (service, _, _, publisher) = BuildService([applicant], []);

        await service.StartAsync(ApprovalApplicationType.Leave, Guid.NewGuid(), applicant.Id, CancellationToken.None);

        await publisher.DidNotReceive().Publish(Arg.Any<ApprovalNotificationRequested>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_FinalApproval_NotifiesApplicant()
    {
        var applicant = BuildEmployee(email: "applicant@test.com", userId: Guid.NewGuid());
        var approver = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        var (service, instances, _, publisher) = BuildService([applicant, approver], [workflow]);

        var applicationId = Guid.NewGuid();
        var instance = await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        // Covers the fields added for the SignalR push channel -- ApplicationType/ApplicationId/
        // ApprovalInstanceId are what the frontend uses to invalidate the exact query keys
        // ApprovalStatusCell/ApprovalTimeline read; RecipientUserId is what the push consumer
        // targets Clients.User(...) with; both Deliver* default true with no preference row.
        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.RecipientEmail == "applicant@test.com" && m.StatusLabel == "Approved" &&
                m.ApplicationType == "Leave" && m.ApplicationId == applicationId &&
                m.ApprovalInstanceId == instance.Id && m.RecipientUserId == applicant.UserId &&
                m.DeliverEmail && m.DeliverPush),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_FinalApproval_RespectsApplicantsNotificationPreference()
    {
        var applicant = BuildEmployee(email: "applicant@test.com", userId: Guid.NewGuid());
        var approver = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        // Applicant opted out of push (but not email) for Leave specifically.
        var preference = new NotificationPreference
        {
            EmployeeId = applicant.Id,
            ApplicationType = ApprovalApplicationType.Leave,
            EmailEnabled = true,
            PushEnabled = false,
        };
        var (service, _, _, publisher) = BuildService([applicant, approver], [workflow], [preference]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.DeliverEmail && !m.DeliverPush),
            Arg.Any<CancellationToken>());
    }

    // An employee matched to their login by email (no invitation-linked UserId) used to get no
    // push at all -- DeliverPush required UserId. The push worker now reaches them by email.
    [Theory]
    [InlineData(ApprovalActionType.Approved, "Approved")]
    [InlineData(ApprovalActionType.Declined, "Declined")]
    public async Task RecordActionAsync_Resolution_PushesToAnApplicantLinkedOnlyByEmail(ApprovalActionType action, string statusLabel)
    {
        var applicant = BuildEmployee(email: "applicant@test.com", userId: null);
        var approver = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        var (service, _, _, publisher) = BuildService([applicant, approver], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, action, note: null, CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.StatusLabel == statusLabel && m.RecipientEmail == "applicant@test.com" &&
                m.RecipientUserId == null && m.DeliverPush),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_StepProgress_PushesToAnApplicantLinkedOnlyByEmail()
    {
        var applicant = BuildEmployee(email: "applicant@test.com", userId: null);
        var first = BuildEmployee();
        var second = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps =
            [
                new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = first.Id, MinApprovals = 1, NamedApprovers = [] },
                new ApprovalWorkflowStep { StepNumber = 2, ApproverType = ApproverType.Person, ApproverEmployeeId = second.Id, MinApprovals = 1, NamedApprovers = [] },
            ],
        };
        var (service, _, _, publisher) = BuildService([applicant, first, second], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, first.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.StatusLabel == ApprovalEngineService.StepApprovedStatusLabel &&
                m.RecipientEmail == "applicant@test.com" && m.DeliverPush),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_ApplicantWithNeitherEmailNorUserId_IsNotPushed()
    {
        var applicant = BuildEmployee(email: null, userId: null);
        var approver = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        var (service, _, _, publisher) = BuildService([applicant, approver], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        await publisher.DidNotReceive().Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.StatusLabel == "Approved"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_Decline_NotifiesApplicantWithNote()
    {
        var applicant = BuildEmployee(email: "applicant@test.com");
        var approver = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NoteRequirement = NoteRequirement.Optional, NamedApprovers = [] }],
        };
        var (service, _, _, publisher) = BuildService([applicant, approver], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Declined, note: "Not enough coverage", CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.RecipientEmail == "applicant@test.com" && m.StatusLabel == "Declined" && m.Note == "Not enough coverage"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_AdvancesToNextStep_NotifiesNextStepApprover()
    {
        var applicant = BuildEmployee();
        var step1Approver = BuildEmployee();
        var step2Approver = BuildEmployee(email: "step2@test.com");
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
        var (service, _, _, publisher) = BuildService([applicant, step1Approver, step2Approver], [workflow]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, step1Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.RecipientEmail == "step2@test.com" && m.StatusLabel == "Pending Your Approval" && m.StepNumber == 2),
            Arg.Any<CancellationToken>());
    }

    private static ApprovalWorkflow TwoStepWorkflow(Employee step1Approver, Employee step2Approver) => new()
    {
        Id = Guid.NewGuid(),
        ApplicationType = ApprovalApplicationType.Overtime,
        IsActive = true,
        Steps =
        [
            new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = step1Approver.Id, MinApprovals = 1, NamedApprovers = [] },
            new ApprovalWorkflowStep { StepNumber = 2, ApproverType = ApproverType.Person, ApproverEmployeeId = step2Approver.Id, MinApprovals = 1, NamedApprovers = [] },
        ],
    };

    // The applicant used to hear nothing between filing and the final decision -- on a 2-step
    // chain, step 1 clearing sent only the step-2 approver a notice, leaving the applicant's
    // bell empty and their list stale until the very end.
    [Fact]
    public async Task RecordActionAsync_IntermediateStepClears_NotifiesApplicantOfProgress()
    {
        var applicant = BuildEmployee(email: "applicant@test.com", userId: Guid.NewGuid());
        var step1Approver = BuildEmployee();
        var step2Approver = BuildEmployee(email: "step2@test.com");
        var (service, _, _, publisher) = BuildService(
            [applicant, step1Approver, step2Approver], [TwoStepWorkflow(step1Approver, step2Approver)]);

        var applicationId = Guid.NewGuid();
        var instance = await service.StartAsync(ApprovalApplicationType.Overtime, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Overtime, applicationId, applicant.Id, step1Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.RecipientEmail == "applicant@test.com" &&
                m.RecipientUserId == applicant.UserId &&
                m.StatusLabel == ApprovalEngineService.StepApprovedStatusLabel &&
                m.StepNumber == 1 && m.TotalSteps == 2 &&
                m.ApplicationType == "Overtime" && m.ApplicationId == applicationId &&
                m.ApprovalInstanceId == instance.Id &&
                m.DeliverEmail && m.DeliverPush),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_FinalStep_SendsResolutionNotProgressNotice()
    {
        var applicant = BuildEmployee(email: "applicant@test.com", userId: Guid.NewGuid());
        var step1Approver = BuildEmployee();
        var step2Approver = BuildEmployee();
        var (service, _, _, publisher) = BuildService(
            [applicant, step1Approver, step2Approver], [TwoStepWorkflow(step1Approver, step2Approver)]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Overtime, applicationId, applicant.Id, CancellationToken.None);
        await service.RecordActionAsync(
            ApprovalApplicationType.Overtime, applicationId, applicant.Id, step1Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Overtime, applicationId, applicant.Id, step2Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "applicant@test.com" && m.StatusLabel == "Approved"),
            Arg.Any<CancellationToken>());
        await publisher.DidNotReceive().Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.StatusLabel == ApprovalEngineService.StepApprovedStatusLabel),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_ProgressNotice_RespectsApplicantsEmailOptOut()
    {
        var applicant = BuildEmployee(email: "applicant@test.com", userId: Guid.NewGuid());
        var step1Approver = BuildEmployee();
        var step2Approver = BuildEmployee();
        var optOut = new NotificationPreference
        {
            EmployeeId = applicant.Id,
            ApplicationType = ApprovalApplicationType.Overtime,
            EmailEnabled = false,
            PushEnabled = true,
        };
        var (service, _, _, publisher) = BuildService(
            [applicant, step1Approver, step2Approver], [TwoStepWorkflow(step1Approver, step2Approver)], [optOut]);

        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Overtime, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Overtime, applicationId, applicant.Id, step1Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.StatusLabel == ApprovalEngineService.StepApprovedStatusLabel && !m.DeliverEmail && m.DeliverPush),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Regression guard for a production 500 ("Duplicate entry ... for key
    /// IX_ApprovalInstances_ApplicationType_ApplicationId"): StartAsync used to unconditionally
    /// insert a new ApprovalInstance, which works the first time but violates that unique index
    /// (one row per ApplicationType+ApplicationId, ever) the moment a second cycle is started for
    /// the same application -- exactly what happens when a DTR/Payroll deletion request is
    /// declined and then re-requested. StartAsync must instead reuse and reset the existing
    /// (now-resolved) row for a new cycle.
    /// </summary>
    [Fact]
    public async Task StartAsync_ExistingDeclinedInstance_ResetsInPlace_InsteadOfInsertingDuplicate()
    {
        var applicant = BuildEmployee();
        var approver = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.PayrollPostingDeletion,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        var (service, instances, _, _) = BuildService([applicant, approver], [workflow]);
        var applicationId = Guid.NewGuid();

        await service.StartAsync(ApprovalApplicationType.PayrollPostingDeletion, applicationId, applicant.Id, CancellationToken.None);
        await service.RecordActionAsync(
            ApprovalApplicationType.PayrollPostingDeletion, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Declined, note: null, CancellationToken.None);
        instances.Should().ContainSingle(i => i.Status == ApprovalInstanceStatus.Declined);

        // Re-requesting deletion calls StartAsync again for the SAME applicationId -- this used
        // to throw a duplicate-key error instead of returning a fresh cycle.
        var act = () => service.StartAsync(ApprovalApplicationType.PayrollPostingDeletion, applicationId, applicant.Id, CancellationToken.None);
        var restarted = await act.Should().NotThrowAsync();

        instances.Should().ContainSingle("the declined row must be reused, not duplicated");
        restarted.Subject.Status.Should().Be(ApprovalInstanceStatus.InProgress);
        restarted.Subject.CurrentStepNumber.Should().Be(1);

        // The restarted cycle must still be actionable, exactly like a brand-new instance.
        var result = await service.RecordActionAsync(
            ApprovalApplicationType.PayrollPostingDeletion, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        result.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task StartAsync_ExistingInProgressInstance_ReturnsSameInstanceUnchanged()
    {
        var applicant = BuildEmployee();
        var (service, instances, _, publisher) = BuildService([applicant], []);
        var applicationId = Guid.NewGuid();

        var first = await service.StartAsync(ApprovalApplicationType.Dtr, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        var second = await service.StartAsync(ApprovalApplicationType.Dtr, applicationId, applicant.Id, CancellationToken.None);

        second.Id.Should().Be(first.Id);
        instances.Should().ContainSingle();
        await publisher.DidNotReceive().Publish(Arg.Any<ApprovalNotificationRequested>(), Arg.Any<CancellationToken>());
    }

    // A member matched to their login only by email (never linked -- e.g. an Owner who created
    // the company rather than accepting an invite) can approve the step, so they're notified too.
    // Only someone with neither a linked login nor an email is unreachable and skipped.
    [Theory]
    [InlineData(ApproverType.Department)]
    [InlineData(ApproverType.Position)]
    [InlineData(ApproverType.ApplicantDepartment)]
    public async Task StartAsync_PoolStep_NotifiesLinkedAndEmailMatchedMembers(ApproverType approverType)
    {
        var departmentId = Guid.NewGuid();
        var positionId = Guid.NewGuid();
        Employee Member(Guid? userId, string? email)
        {
            var member = BuildEmployee(userId: userId, departmentId: departmentId, email: email);
            member.PositionId = positionId;
            return member;
        }
        var applicant = BuildEmployee(departmentId: departmentId);
        var linked = Member(Guid.NewGuid(), "member@test.com");
        var emailMatched = Member(null, "owner@test.com");
        var unreachable = Member(null, null);
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps =
            [
                new ApprovalWorkflowStep
                {
                    StepNumber = 1, ApproverType = approverType, MinApprovals = 1, NamedApprovers = [],
                    ApproverDepartmentId = departmentId, ApproverPositionId = positionId,
                },
            ],
        };
        var (service, _, _, publisher) = BuildService([applicant, linked, emailMatched, unreachable], [workflow]);

        await service.StartAsync(ApprovalApplicationType.Leave, Guid.NewGuid(), applicant.Id, CancellationToken.None);

        await publisher.Received(1).Publish(Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "member@test.com"), Arg.Any<CancellationToken>());
        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "owner@test.com" && m.RecipientUserId == null && m.DeliverPush),
            Arg.Any<CancellationToken>());
        await publisher.DidNotReceive().Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "" && m.RecipientUserId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_PersonStep_NotifiesAnEmailMatchedOwner()
    {
        var applicant = BuildEmployee();
        var owner = BuildEmployee(userId: null, email: "owner@test.com");
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = owner.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        var (service, _, _, publisher) = BuildService([applicant, owner], [workflow]);

        await service.StartAsync(ApprovalApplicationType.Leave, Guid.NewGuid(), applicant.Id, CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.RecipientEmail == "owner@test.com" && m.DeliverPush &&
                m.StatusLabel == ApprovalEngineService.PendingApprovalStatusLabel),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReassignApproverAsync_OriginalApprover_NoLongerEligible_OnlyReassigneeIs()
    {
        var applicant = BuildEmployee();
        var originalApprover = BuildEmployee();
        var reassignee = BuildEmployee(email: "reassignee@test.com");
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = originalApprover.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        var (service, _, _, _) = BuildService([applicant, originalApprover, reassignee], [workflow]);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);

        await service.ReassignApproverAsync(
            ApprovalApplicationType.Leave, applicationId, reassignee.Id, applicant.Id, "Out of office", CancellationToken.None);

        (await service.IsCallerEligibleAsync(ApprovalApplicationType.Leave, applicationId, reassignee.Id, CancellationToken.None))
            .Should().BeTrue();
        (await service.IsCallerEligibleAsync(ApprovalApplicationType.Leave, applicationId, originalApprover.Id, CancellationToken.None))
            .Should().BeFalse("the original step-configured approver was reassigned away");
    }

    [Fact]
    public async Task ReassignApproverAsync_Reassignee_CanRecordAction_AdvancingTheWorkflow()
    {
        var applicant = BuildEmployee();
        var originalApprover = BuildEmployee();
        var reassignee = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = originalApprover.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        var (service, _, actions, _) = BuildService([applicant, originalApprover, reassignee], [workflow]);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        await service.ReassignApproverAsync(
            ApprovalApplicationType.Leave, applicationId, reassignee.Id, applicant.Id, null, CancellationToken.None);

        actions.Should().ContainSingle(a => a.Action == ApprovalActionType.Reassigned && a.ActorEmployeeId == applicant.Id);

        var result = await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, reassignee.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        result.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task ReassignApproverAsync_OverrideClears_OnceStepResolves_DoesNotLeakIntoNextStep()
    {
        var applicant = BuildEmployee();
        var step1Original = BuildEmployee();
        var step1Reassignee = BuildEmployee();
        var step2Approver = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps =
            [
                new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = step1Original.Id, MinApprovals = 1, NamedApprovers = [] },
                new ApprovalWorkflowStep { StepNumber = 2, ApproverType = ApproverType.Person, ApproverEmployeeId = step2Approver.Id, MinApprovals = 1, NamedApprovers = [] },
            ],
        };
        var (service, _, _, _) = BuildService([applicant, step1Original, step1Reassignee, step2Approver], [workflow]);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        await service.ReassignApproverAsync(
            ApprovalApplicationType.Leave, applicationId, step1Reassignee.Id, applicant.Id, null, CancellationToken.None);

        await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, step1Reassignee.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        // Step 1's reassignment must not carry over -- only step 2's own configured approver
        // (not the earlier reassignee) should be eligible now.
        (await service.IsCallerEligibleAsync(ApprovalApplicationType.Leave, applicationId, step2Approver.Id, CancellationToken.None))
            .Should().BeTrue();
        (await service.IsCallerEligibleAsync(ApprovalApplicationType.Leave, applicationId, step1Reassignee.Id, CancellationToken.None))
            .Should().BeFalse("the step 1 reassignment must not leak into step 2");
    }

    [Fact]
    public async Task ReassignApproverAsync_AlreadyResolvedInstance_Throws()
    {
        var applicant = BuildEmployee();
        var approver = BuildEmployee();
        var reassignee = BuildEmployee();
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            ApplicationType = ApprovalApplicationType.Leave,
            IsActive = true,
            Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NamedApprovers = [] }],
        };
        var (service, _, _, _) = BuildService([applicant, approver, reassignee], [workflow]);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        var act = () => service.ReassignApproverAsync(
            ApprovalApplicationType.Leave, applicationId, reassignee.Id, applicant.Id, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // An application edited after an approver already signed off goes back to the first approver
    // -- otherwise that approval would carry over to content they never saw.
    [Fact]
    public async Task RestartIfProgressedAsync_AfterStep1Approval_ResetsToStep1InANewCycle_AndRenotifiesFirstApprover()
    {
        var applicant = BuildEmployee();
        var step1Approver = BuildEmployee(email: "step1@test.com");
        var step2Approver = BuildEmployee();
        var (service, instances, _, publisher) = BuildService(
            [applicant, step1Approver, step2Approver], [TwoStepWorkflow(step1Approver, step2Approver)]);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Overtime, applicationId, applicant.Id, CancellationToken.None);
        await service.RecordActionAsync(
            ApprovalApplicationType.Overtime, applicationId, applicant.Id, step1Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        var cycleBefore = instances.Single().CycleNumber;
        publisher.ClearReceivedCalls();

        var restarted = await service.RestartIfProgressedAsync(ApprovalApplicationType.Overtime, applicationId, CancellationToken.None);

        restarted.Should().BeTrue();
        var instance = instances.Single();
        instance.CurrentStepNumber.Should().Be(1);
        instance.CycleNumber.Should().Be(cycleBefore + 1);
        instance.Status.Should().Be(ApprovalInstanceStatus.InProgress);
        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "step1@test.com" && m.StepNumber == 1),
            Arg.Any<CancellationToken>());
    }

    // The previous cycle's step-1 approval must neither block the approver from re-approving
    // (duplicate guard) nor count toward the new cycle's quorum.
    [Fact]
    public async Task RestartIfProgressedAsync_FirstApproverMustApproveAgain_InTheNewCycle()
    {
        var applicant = BuildEmployee();
        var step1Approver = BuildEmployee();
        var step2Approver = BuildEmployee();
        var (service, instances, _, _) = BuildService(
            [applicant, step1Approver, step2Approver], [TwoStepWorkflow(step1Approver, step2Approver)]);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Overtime, applicationId, applicant.Id, CancellationToken.None);
        await service.RecordActionAsync(
            ApprovalApplicationType.Overtime, applicationId, applicant.Id, step1Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);
        await service.RestartIfProgressedAsync(ApprovalApplicationType.Overtime, applicationId, CancellationToken.None);

        var result = await service.RecordActionAsync(
            ApprovalApplicationType.Overtime, applicationId, applicant.Id, step1Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        result.CurrentStepNumber.Should().Be(2, "the re-approval in the new cycle clears step 1 again");
        instances.Single().Actions.Count(a => a.ActorEmployeeId == step1Approver.Id).Should().Be(2);
    }

    [Fact]
    public async Task RestartIfProgressedAsync_NothingApprovedYet_LeavesInstanceAlone()
    {
        var applicant = BuildEmployee();
        var step1Approver = BuildEmployee();
        var step2Approver = BuildEmployee();
        var (service, instances, _, publisher) = BuildService(
            [applicant, step1Approver, step2Approver], [TwoStepWorkflow(step1Approver, step2Approver)]);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Overtime, applicationId, applicant.Id, CancellationToken.None);
        var cycleBefore = instances.Single().CycleNumber;
        publisher.ClearReceivedCalls();

        var restarted = await service.RestartIfProgressedAsync(ApprovalApplicationType.Overtime, applicationId, CancellationToken.None);

        restarted.Should().BeFalse();
        instances.Single().CycleNumber.Should().Be(cycleBefore);
        await publisher.DidNotReceive().Publish(Arg.Any<ApprovalNotificationRequested>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestartIfProgressedAsync_ResolvedInstance_IsNotReopened()
    {
        var applicant = BuildEmployee();
        var step1Approver = BuildEmployee();
        var step2Approver = BuildEmployee();
        var (service, instances, _, _) = BuildService(
            [applicant, step1Approver, step2Approver], [TwoStepWorkflow(step1Approver, step2Approver)]);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Overtime, applicationId, applicant.Id, CancellationToken.None);
        await service.RecordActionAsync(
            ApprovalApplicationType.Overtime, applicationId, applicant.Id, step1Approver.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Declined, note: null, CancellationToken.None);

        var restarted = await service.RestartIfProgressedAsync(ApprovalApplicationType.Overtime, applicationId, CancellationToken.None);

        restarted.Should().BeFalse();
        instances.Single().Status.Should().Be(ApprovalInstanceStatus.Declined);
    }

    // ---- the person who caused an event isn't notified about it ----

    private static ApprovalWorkflow SingleStepWorkflow(Employee approver) => new()
    {
        Id = Guid.NewGuid(),
        ApplicationType = ApprovalApplicationType.Leave,
        IsActive = true,
        Steps = [new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1, NoteRequirement = NoteRequirement.Optional, NamedApprovers = [] }],
    };

    [Fact]
    public async Task RecordActionAsync_ApplicantApprovesOwnRequest_IsNotNotified()
    {
        var applicant = BuildEmployee(email: "me@test.com");
        var (service, _, _, publisher) = BuildService([applicant], [SingleStepWorkflow(applicant)]);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, applicant.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        await publisher.DidNotReceive().Publish(Arg.Any<ApprovalNotificationRequested>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_ApplicantDeclinesOwnRequest_IsNotNotified()
    {
        var applicant = BuildEmployee(email: "me@test.com");
        var (service, _, _, publisher) = BuildService([applicant], [SingleStepWorkflow(applicant)]);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Leave, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicant.Id, applicant.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Declined, note: null, CancellationToken.None);

        await publisher.DidNotReceive().Publish(Arg.Any<ApprovalNotificationRequested>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_FilerWhoIsTheFirstStepApprover_IsNotToldPendingTheirOwnApproval()
    {
        var applicant = BuildEmployee(email: "me@test.com");
        var (service, _, _, publisher) = BuildService([applicant], [SingleStepWorkflow(applicant)]);

        await service.StartAsync(ApprovalApplicationType.Leave, Guid.NewGuid(), applicant.Id, CancellationToken.None);

        await publisher.DidNotReceive().Publish(Arg.Any<ApprovalNotificationRequested>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_FiledOnBehalf_StillNotifiesTheApproverWhoIsNotTheFiler()
    {
        var applicant = BuildEmployee();
        var approver = BuildEmployee(email: "approver@test.com");
        var (service, _, _, publisher) = BuildService([applicant, approver], [SingleStepWorkflow(approver)]);

        await service.StartAsync(ApprovalApplicationType.Leave, Guid.NewGuid(), applicant.Id, CancellationToken.None, actorEmployeeId: Guid.NewGuid());

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "approver@test.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_FiledOnBehalfByTheApprover_DoesNotNotifyThatApprover()
    {
        var applicant = BuildEmployee();
        var approver = BuildEmployee(email: "approver@test.com");
        var (service, _, _, publisher) = BuildService([applicant, approver], [SingleStepWorkflow(approver)]);

        await service.StartAsync(ApprovalApplicationType.Leave, Guid.NewGuid(), applicant.Id, CancellationToken.None, actorEmployeeId: approver.Id);

        await publisher.DidNotReceive().Publish(Arg.Any<ApprovalNotificationRequested>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_StepOneApproverAlsoStepTwoApprover_IsNotToldPendingAfterTheirOwnApproval()
    {
        var applicant = BuildEmployee(email: "applicant@test.com");
        var both = BuildEmployee(email: "both@test.com");
        var (service, _, _, publisher) = BuildService([applicant, both], [TwoStepWorkflow(both, both)]);
        var applicationId = Guid.NewGuid();
        await service.StartAsync(ApprovalApplicationType.Overtime, applicationId, applicant.Id, CancellationToken.None);
        publisher.ClearReceivedCalls();

        await service.RecordActionAsync(
            ApprovalApplicationType.Overtime, applicationId, applicant.Id, both.Id,
            callerHasOverrideAccess: false, ApprovalActionType.Approved, note: null, CancellationToken.None);

        await publisher.DidNotReceive().Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "both@test.com"),
            Arg.Any<CancellationToken>());
        // The applicant is still told their request moved forward.
        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "applicant@test.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyApplicantAsync_WhenTheApplicantIsTheActor_PublishesNothing()
    {
        var applicant = BuildEmployee(email: "me@test.com");
        var (service, _, _, publisher) = BuildService([applicant], []);

        await service.NotifyApplicantAsync(
            ApprovalApplicationType.Dtr, Guid.NewGuid(), applicant.Id, "Draft Saved", note: null,
            CancellationToken.None, actorEmployeeId: applicant.Id);

        await publisher.DidNotReceive().Publish(Arg.Any<ApprovalNotificationRequested>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyApplicantAsync_WhenSomeoneElseActed_StillNotifiesTheApplicant()
    {
        var applicant = BuildEmployee(email: "me@test.com");
        var (service, _, _, publisher) = BuildService([applicant], []);

        await service.NotifyApplicantAsync(
            ApprovalApplicationType.PassSlip, Guid.NewGuid(), applicant.Id, ApprovalEngineService.CancelledStatusLabel, note: null,
            CancellationToken.None, actorEmployeeId: Guid.NewGuid());

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "me@test.com"),
            Arg.Any<CancellationToken>());
    }
}
