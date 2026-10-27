using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Onepunch.Common.Lib.DTO;
using Onepunch.Common.Lib.Exceptions;

namespace hrms.test.ServiceTests;

/// <summary>
/// "While I'm away" delegation: a delegate may act wherever the delegator could have -- as that
/// person, never twice for one step -- for the dates, type and leave condition the delegator chose;
/// and the delegate sees those requests in My Approvals and is told about them.
/// </summary>
public class ApprovalDelegationTests : IDisposable
{
    private readonly SqliteHrmsContext _db = new();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _leaveTypeId = Guid.NewGuid();
    private int _bioId;

    // Ana files requests; Ben is her approver; Cara covers for Ben; Dan is an unrelated colleague.
    private Employee _ana = null!, _ben = null!, _cara = null!, _dan = null!;

    public ApprovalDelegationTests()
    {
        using var seed = _db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = _groupId, Name = "Main" });
        seed.Leaves.Add(new Leave { Id = _leaveTypeId, Code = "VL", Description = "Vacation Leave" });
        _ana = NewEmployee("Ana", "Reyes", withLogin: true);
        _ben = NewEmployee("Ben", "Cruz", withLogin: true);
        _cara = NewEmployee("Cara", "Lim", withLogin: true);
        _dan = NewEmployee("Dan", "Yu", withLogin: true);
        seed.Employees.AddRange(_ana, _ben, _cara, _dan);
        seed.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private Employee NewEmployee(string first, string last, bool withLogin) => new()
    {
        Id = Guid.NewGuid(), PayrollGroupId = _groupId, JobLevel = "", FirstName = first, LastName = last,
        BioId = ++_bioId, EmployeeNo = $"E{_bioId:000}", Email = $"{first.ToLower()}@acme.com",
        UserId = withLogin ? Guid.NewGuid() : null,
    };

    private static DateOnly Today => PhTime.Today(DateTime.UtcNow);

    // ---- pure rules ----

    private static ApprovalDelegation Delegation(DateOnly? from = null, DateOnly? to = null, ApprovalApplicationType? type = null,
        bool onLeave = false, bool active = true) => new()
    {
        DelegatorEmployeeId = Guid.NewGuid(), DelegateEmployeeId = Guid.NewGuid(),
        FromDate = from, ToDate = to, ApplicationType = type, OnlyWhileOnLeave = onLeave, IsActive = active,
    };

    [Fact]
    public void Rules_WindowTypeActiveAndLeave()
    {
        var today = new DateOnly(2026, 10, 10);

        ApprovalDelegationRules.IsInForce(Delegation(), ApprovalApplicationType.Leave, today, false).Should().BeTrue("open-ended applies now");
        ApprovalDelegationRules.IsInForce(Delegation(from: today), ApprovalApplicationType.Leave, today, false).Should().BeTrue();
        ApprovalDelegationRules.IsInForce(Delegation(to: today), ApprovalApplicationType.Leave, today, false).Should().BeTrue("the end date is inclusive");
        ApprovalDelegationRules.IsInForce(Delegation(from: today.AddDays(1)), ApprovalApplicationType.Leave, today, false).Should().BeFalse("not started");
        ApprovalDelegationRules.IsInForce(Delegation(to: today.AddDays(-1)), ApprovalApplicationType.Leave, today, false).Should().BeFalse("ended");
        ApprovalDelegationRules.IsInForce(Delegation(active: false), ApprovalApplicationType.Leave, today, false).Should().BeFalse();
        ApprovalDelegationRules.IsInForce(Delegation(type: ApprovalApplicationType.Overtime), ApprovalApplicationType.Leave, today, false).Should().BeFalse();
        ApprovalDelegationRules.IsInForce(Delegation(type: ApprovalApplicationType.Leave), ApprovalApplicationType.Leave, today, false).Should().BeTrue();
        ApprovalDelegationRules.IsInForce(Delegation(onLeave: true), ApprovalApplicationType.Leave, today, delegatorOnLeave: false).Should().BeFalse();
        ApprovalDelegationRules.IsInForce(Delegation(onLeave: true), ApprovalApplicationType.Leave, today, delegatorOnLeave: true).Should().BeTrue();
    }

    // ---- pure eligibility ----

    private static ApprovalWorkflowStep PersonStep(Guid approverId) =>
        new() { StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = approverId, MinApprovals = 1, NamedApprovers = [] };

    [Fact]
    public void Resolver_ADelegateIsEligibleWhereTheDelegatorWould_ActingForThem()
    {
        var step = PersonStep(_ben.Id);
        var context = new ApprovalEligibilityContext(_cara, _ana, step, [], null, [_ben]);

        var standing = new ApproverEligibilityResolver().Resolve(context);

        standing.Eligible.Should().BeTrue();
        standing.OnBehalfOfEmployeeId.Should().Be(_ben.Id);
    }

    [Fact]
    public void Resolver_ADirectApproverActsAsThemself_EvenWhenTheyAreAlsoADelegate()
    {
        var step = PersonStep(_cara.Id);
        var context = new ApprovalEligibilityContext(_cara, _ana, step, [], null, [_ben]);

        var standing = new ApproverEligibilityResolver().Resolve(context);

        standing.Should().Be(ApprovalStanding.Direct);
    }

    [Fact]
    public void Resolver_WithoutDelegationOrForAnotherPersonsStep_IsNotEligible()
    {
        var resolver = new ApproverEligibilityResolver();

        resolver.Resolve(new ApprovalEligibilityContext(_cara, _ana, PersonStep(_ben.Id), [], null, [])).Eligible.Should().BeFalse();
        resolver.Resolve(new ApprovalEligibilityContext(_cara, _ana, PersonStep(_dan.Id), [], null, [_ben])).Eligible.Should().BeFalse(
            "delegating Ben's approvals doesn't cover Dan's step");
    }

    [Fact]
    public void Resolver_AReassignedStepStaysExclusive_ToDelegatesToo()
    {
        var context = new ApprovalEligibilityContext(_cara, _ana, PersonStep(_ben.Id), [], ReassignedApproverEmployeeId: _dan.Id, [_ben, _dan]);

        new ApproverEligibilityResolver().Resolve(context).Eligible.Should().BeFalse();
    }

    // ---- service ----

    private async Task WithServiceAsync(Func<ApprovalDelegationService, Task> act)
    {
        await using var context = _db.NewContext();
        await act(new ApprovalDelegationService(new UnitOfWorkService(context)));
    }

    private async Task<ApprovalDelegation> AddDelegationAsync(Employee delegator, Employee delegateTo, DateOnly? from = null, DateOnly? to = null,
        ApprovalApplicationType? type = null, bool onLeave = false, bool active = true)
    {
        var delegation = new ApprovalDelegation
        {
            Id = Guid.NewGuid(), DelegatorEmployeeId = delegator.Id, DelegateEmployeeId = delegateTo.Id,
            FromDate = from, ToDate = to, ApplicationType = type, OnlyWhileOnLeave = onLeave, IsActive = active,
        };
        await using var seed = _db.NewContext();
        seed.ApprovalDelegations.Add(delegation);
        await seed.SaveChangesAsync();
        return delegation;
    }

    private async Task ApprovedLeaveAsync(Employee employee, DateOnly from, DateOnly to)
    {
        await using var seed = _db.NewContext();
        seed.leaveApplications.Add(new LeaveApplication
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, LeaveId = _leaveTypeId, Leave = null!,
            DurationType = DurationType.MultiDay, LeaveDateFrom = from, LeaveDateTo = to,
            ApprovalStatus = ApprovalStatus.Approved,
        });
        await seed.SaveChangesAsync();
    }

    [Fact]
    public async Task ActiveDelegators_AreThoseInForceTodayForTheType()
    {
        await AddDelegationAsync(_ben, _cara);
        await AddDelegationAsync(_dan, _cara, to: Today.AddDays(-1));                       // ended
        await AddDelegationAsync(_ana, _cara, type: ApprovalApplicationType.Overtime);       // other type

        IReadOnlyList<Guid> ids = [];
        await WithServiceAsync(async s => ids = await s.GetActiveDelegatorIdsAsync(_cara.Id, ApprovalApplicationType.Leave, default));

        ids.Should().Equal(_ben.Id);
    }

    [Fact]
    public async Task WhileOnLeaveDelegation_NeedsAnApprovedLeaveCoveringToday()
    {
        await AddDelegationAsync(_ben, _cara, onLeave: true);

        IReadOnlyList<Guid> before = [];
        await WithServiceAsync(async s => before = await s.GetActiveDelegatorIdsAsync(_cara.Id, ApprovalApplicationType.Leave, default));
        before.Should().BeEmpty("Ben isn't on leave");

        await ApprovedLeaveAsync(_ben, Today.AddDays(-1), Today.AddDays(2));

        IReadOnlyList<Guid> during = [];
        await WithServiceAsync(async s => during = await s.GetActiveDelegatorIdsAsync(_cara.Id, ApprovalApplicationType.Leave, default));
        during.Should().Equal(_ben.Id);
    }

    [Fact]
    public async Task ActiveDelegates_AreFoundForTheDelegators()
    {
        await AddDelegationAsync(_ben, _cara);
        await AddDelegationAsync(_ben, _dan, active: false);

        IReadOnlyList<(Guid DelegatorId, Guid DelegateId)> pairs = [];
        await WithServiceAsync(async s => pairs = await s.GetActiveDelegatesAsync([_ben.Id], ApprovalApplicationType.Leave, default));

        pairs.Should().Equal((_ben.Id, _cara.Id));
    }

    [Fact]
    public async Task Creating_RejectsSelfBadDatesAndADelegateWithoutALogin()
    {
        var noLogin = NewEmployee("Eve", "Ong", withLogin: false);
        await using (var seed = _db.NewContext())
        {
            seed.Employees.Add(noLogin);
            await seed.SaveChangesAsync();
        }

        ApprovalDelegation Make(Guid delegateId, DateOnly? from = null, DateOnly? to = null) => new()
        { DelegatorEmployeeId = _ben.Id, DelegateEmployeeId = delegateId, FromDate = from, ToDate = to };

        await FluentActions.Awaiting(() => WithServiceAsync(s => s.CreateDelegationAsync(Make(_ben.Id))))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*yourself*");
        await FluentActions.Awaiting(() => WithServiceAsync(s => s.CreateDelegationAsync(Make(_cara.Id, Today.AddDays(3), Today))))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*before the start*");
        await FluentActions.Awaiting(() => WithServiceAsync(s => s.CreateDelegationAsync(Make(_cara.Id, null, Today.AddDays(-2)))))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already passed*");
        await FluentActions.Awaiting(() => WithServiceAsync(s => s.CreateDelegationAsync(Make(noLogin.Id))))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*login*");

        await WithServiceAsync(s => s.CreateDelegationAsync(Make(_cara.Id, Today, Today.AddDays(5))));
        await using var read = _db.NewContext();
        (await read.ApprovalDelegations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Deactivating_OnlyWorksOnYourOwn_UnlessAdmin()
    {
        var delegation = await AddDelegationAsync(_ben, _cara);

        await FluentActions.Awaiting(() => WithServiceAsync(s => s.DeactivateAsync(delegation.Id, onlyForDelegator: _dan.Id)))
            .Should().ThrowAsync<NotFoundException>();

        await WithServiceAsync(s => s.DeactivateAsync(delegation.Id, onlyForDelegator: _ben.Id));
        await using var read = _db.NewContext();
        (await read.ApprovalDelegations.AsNoTracking().SingleAsync()).IsActive.Should().BeFalse();
    }

    // ---- engine + My Approvals, end to end on SQLite ----

    private sealed class FakeAdapter : IApprovalRequestAdapter
    {
        public ApprovalApplicationType Type => ApprovalApplicationType.Leave;
        public string PermissionPrefix => "Leave";
        public Task<Dictionary<Guid, ApprovalRequestSummary>> DescribeAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) =>
            Task.FromResult(ids.ToDictionary(id => id, _ => new ApprovalRequestSummary("Vacation Leave · Oct 5", DateTime.UtcNow)));
        public Task ApproveAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) => Task.CompletedTask;
        public Task DeclineAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) => Task.CompletedTask;
    }

    private async Task<(Guid WorkflowId, Guid ApplicationId)> SeedPendingLeaveAsync(int minApprovals = 1, params Employee[] approvers)
    {
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = "Leave flow", IsActive = true,
            Steps = [new ApprovalWorkflowStep
            {
                Id = Guid.NewGuid(), StepNumber = 1,
                ApproverType = approvers.Length > 1 ? ApproverType.Position : ApproverType.Person,
                ApproverEmployeeId = approvers.Length == 1 ? approvers[0].Id : null,
                MinApprovals = minApprovals,
            }],
        };
        var applicationId = Guid.NewGuid();
        await using var seed = _db.NewContext();
        seed.ApprovalWorkflows.Add(workflow);
        seed.ApprovalInstances.Add(new ApprovalInstance
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, ApplicationId = applicationId,
            ApplicantEmployeeId = _ana.Id, ApprovalWorkflowId = workflow.Id, CurrentStepNumber = 1,
        });
        await seed.SaveChangesAsync();
        return (workflow.Id, applicationId);
    }

    private (ApprovalEngineService Engine, IPublishEndpoint Publisher, UnitOfWorkService Uow, HrmsContext Context) NewEngine()
    {
        var context = _db.NewContext();
        var uow = new UnitOfWorkService(context);
        var delegations = new ApprovalDelegationService(uow);
        var publisher = Substitute.For<IPublishEndpoint>();
        return (new ApprovalEngineService(uow, publisher, null, delegations), publisher, uow, context);
    }

    [Fact]
    public async Task MyApprovals_ListsTheRequestForTheApprover_AndForTheirDelegate_ButNotForOthers()
    {
        var (_, applicationId) = await SeedPendingLeaveAsync(1, _ben);
        await AddDelegationAsync(_ben, _cara);

        async Task<List<MyApprovalItem>> PendingFor(Employee who)
        {
            var (engine, _, uow, context) = NewEngine();
            await using var _ = context;
            var service = new MyApprovalsService(uow, engine, [new FakeAdapter()]);
            return await service.GetPendingAsync(who.Id, prefix => prefix == "Leave", default);
        }

        var ben = await PendingFor(_ben);
        var cara = await PendingFor(_cara);
        var dan = await PendingFor(_dan);

        ben.Should().ContainSingle().Which.OnBehalfOfEmployeeId.Should().BeNull();
        var viaDelegation = cara.Should().ContainSingle().Subject;
        viaDelegation.ApplicationId.Should().Be(applicationId);
        viaDelegation.OnBehalfOfEmployeeId.Should().Be(_ben.Id);
        viaDelegation.OnBehalfOfName.Should().Be("Ben Cruz");
        dan.Should().BeEmpty();
    }

    [Fact]
    public async Task MyApprovals_DirectApproverNeedsThePermission_ButADelegateDoesNot()
    {
        await SeedPendingLeaveAsync(1, _ben);
        await AddDelegationAsync(_ben, _cara);

        async Task<int> CountFor(Employee who, bool hasPermission)
        {
            var (engine, _, uow, context) = NewEngine();
            await using var _ = context;
            var service = new MyApprovalsService(uow, engine, [new FakeAdapter()]);
            return (await service.GetPendingAsync(who.Id, _ => hasPermission, default)).Count;
        }

        (await CountFor(_ben, hasPermission: false)).Should().Be(0);
        (await CountFor(_cara, hasPermission: false)).Should().Be(1);
    }

    [Fact]
    public async Task ADelegateApproving_RecordsOnBehalfOf_AndClearsTheStep()
    {
        var (_, applicationId) = await SeedPendingLeaveAsync(1, _ben);
        await AddDelegationAsync(_ben, _cara);

        var (engine, _, uow, context) = NewEngine();
        await using (context)
        {
            var result = await engine.RecordActionAsync(
                ApprovalApplicationType.Leave, applicationId, _ana.Id, _cara.Id, callerHasOverrideAccess: false,
                ApprovalActionType.Approved, note: null, default);
            await uow.CommitChangesAsync(default);
            result.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
        }

        await using var read = _db.NewContext();
        var action = await read.ApprovalActions.AsNoTracking().SingleAsync();
        action.ActorEmployeeId.Should().Be(_cara.Id);
        action.OnBehalfOfEmployeeId.Should().Be(_ben.Id);
    }

    [Fact]
    public async Task ANonDelegate_CannotApproveSomeoneElsesStep()
    {
        var (_, applicationId) = await SeedPendingLeaveAsync(1, _ben);

        var (engine, _, _, context) = NewEngine();
        await using (context)
        {
            var act = () => engine.RecordActionAsync(
                ApprovalApplicationType.Leave, applicationId, _ana.Id, _cara.Id, callerHasOverrideAccess: false,
                ApprovalActionType.Approved, null, default);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }
    }

    [Fact]
    public async Task ADelegatorAndTheirDelegate_CountOnceTowardsAQuorum_AndTheSecondCannotRepeat()
    {
        // Two-approval step over Ben and Dan. Cara covers for Ben.
        var (workflowId, applicationId) = await SeedPendingLeaveAsync(2, _ben, _dan);
        await using (var edit = _db.NewContext())
        {
            // Position-type steps resolve by position; model the pool as a Person-less Position step
            // by giving Ben and Dan the same position.
            var position = new Position { Id = Guid.NewGuid(), Name = "Approver", Code = "APR" };
            edit.Positions.Add(position);
            await edit.SaveChangesAsync();
            await edit.Employees.Where(e => e.Id == _ben.Id || e.Id == _dan.Id).ExecuteUpdateAsync(s => s.SetProperty(e => e.PositionId, position.Id));
            await edit.ApprovalWorkflowSteps.Where(s => s.ApprovalWorkflowId == workflowId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ApproverPositionId, position.Id));
        }
        await AddDelegationAsync(_ben, _cara);

        async Task<ApprovalActionResult> Act(Employee who)
        {
            var (engine, _, uow, context) = NewEngine();
            await using var _ = context;
            var result = await engine.RecordActionAsync(
                ApprovalApplicationType.Leave, applicationId, _ana.Id, who.Id, false, ApprovalActionType.Approved, null, default);
            await uow.CommitChangesAsync(default);
            return result;
        }

        (await Act(_cara)).InstanceStatus.Should().Be(ApprovalInstanceStatus.InProgress, "one of two approvals");
        await using (var read = _db.NewContext())
            (await read.ApprovalActions.AsNoTracking().SingleAsync()).OnBehalfOfEmployeeId.Should().Be(_ben.Id);

        // Ben already decided through Cara, so he can't add a second approval of his own.
        await Act(_ben);
        await using (var read = _db.NewContext())
            (await read.ApprovalActions.CountAsync()).Should().Be(1);

        (await Act(_dan)).InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved, "Dan is the second distinct approver");
    }

    [Fact]
    public async Task StepNotifications_AlsoReachTheDelegate_WithAnote()
    {
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = "Leave flow", IsActive = true,
            Steps = [new ApprovalWorkflowStep { Id = Guid.NewGuid(), StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = _ben.Id, MinApprovals = 1 }],
        };
        await using (var seed = _db.NewContext())
        {
            seed.ApprovalWorkflows.Add(workflow);
            await seed.SaveChangesAsync();
        }
        await AddDelegationAsync(_ben, _cara);

        var (engine, publisher, _, context) = NewEngine();
        await using (context)
        {
            await engine.StartAsync(ApprovalApplicationType.Leave, Guid.NewGuid(), _ana.Id, default);
        }

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "ben@acme.com" && m.Note == null), Arg.Any<CancellationToken>());
        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "cara@acme.com" && m.Note == "Delegated from Ben Cruz"),
            Arg.Any<CancellationToken>());
    }
}
