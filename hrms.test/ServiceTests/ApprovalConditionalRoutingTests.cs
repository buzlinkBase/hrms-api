using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Hrms.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Onepunch.Common.Lib.DTO;

namespace hrms.test.ServiceTests;

/// <summary>
/// Conditional routing: a request's own measures (days, hours, amount, leave type) decide which steps
/// it goes through -- a step that only applies "from 5 days" is skipped for a 2-day leave -- and
/// which workflow it runs on (a workflow scoped to a leave type). Edits re-route.
/// </summary>
public class ApprovalConditionalRoutingTests : IDisposable
{
    // ---- measures per request type ----

    [Fact]
    public void Leave_CountsCalendarDays_HalvedForAHalfDay()
    {
        var leaveType = Guid.NewGuid();

        ApprovalRequestAttributes.ForLeave(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7), DayFraction.FullDay, leaveType)
            .Should().Be(new ApprovalRequestAttributes(Days: 3m, LeaveTypeId: leaveType));
        ApprovalRequestAttributes.ForLeave(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5), DayFraction.AM, leaveType).Days
            .Should().Be(0.5m);
    }

    [Fact]
    public void Overtime_UsesTheManualEntry_ElseTheSpanBetweenStartAndEnd()
    {
        ApprovalRequestAttributes.ForOvertime(90, null, null).Hours.Should().Be(1.5m);
        ApprovalRequestAttributes.ForOvertime(0, new DateTime(2026, 10, 5, 18, 0, 0), new DateTime(2026, 10, 5, 21, 30, 0)).Hours
            .Should().Be(3.5m);
        ApprovalRequestAttributes.ForOvertime(0, null, null).Hours.Should().Be(0m);
    }

    [Fact]
    public void OfficialBusiness_HasDaysAndCost_PassSlipHours_LoanAmount()
    {
        var ob = ApprovalRequestAttributes.ForOfficialBusiness(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6), 1500);
        ob.Days.Should().Be(2m);
        ob.Amount.Should().Be(1500m);

        ApprovalRequestAttributes.ForPassSlip(new DateTime(2026, 10, 5, 9, 0, 0), new DateTime(2026, 10, 5, 11, 30, 0)).Hours
            .Should().Be(2.5m);
        ApprovalRequestAttributes.ForPassSlip(new DateTime(2026, 10, 5, 9, 0, 0), null).Hours.Should().Be(0m);

        ApprovalRequestAttributes.ForLoan(25000m).Amount.Should().Be(25000m);
    }

    [Fact]
    public void Attributes_RoundTripThroughJson_AndBadJsonReadsAsNothing()
    {
        var original = new ApprovalRequestAttributes(5m, 2m, 100m, Guid.NewGuid());

        ApprovalRequestAttributes.FromJson(original.ToJson()).Should().Be(original);
        ApprovalRequestAttributes.FromJson(null).Should().BeNull();
        ApprovalRequestAttributes.FromJson("not json").Should().BeNull();
    }

    // ---- when a step applies ----

    private static ApprovalWorkflowStep Cond(ApprovalStepConditionMetric metric, ApprovalConditionOperator op, decimal? value) => new()
    {
        StepNumber = 2, ApproverType = ApproverType.Person, ConditionMetric = metric, ConditionOperator = op, ConditionValue = value,
    };

    [Theory]
    [InlineData(ApprovalConditionOperator.GreaterOrEqual, 5, true)]
    [InlineData(ApprovalConditionOperator.GreaterOrEqual, 4.9, false)]
    [InlineData(ApprovalConditionOperator.GreaterThan, 5, false)]
    [InlineData(ApprovalConditionOperator.GreaterThan, 5.1, true)]
    [InlineData(ApprovalConditionOperator.LessOrEqual, 5, true)]
    [InlineData(ApprovalConditionOperator.LessThan, 5, false)]
    [InlineData(ApprovalConditionOperator.LessThan, 4, true)]
    public void AStepApplies_WhenTheMeasurePassesTheThreshold(ApprovalConditionOperator op, double days, bool applies)
    {
        var step = Cond(ApprovalStepConditionMetric.Days, op, 5m);

        ApprovalStepCondition.Applies(step, new ApprovalRequestAttributes(Days: (decimal)days)).Should().Be(applies);
    }

    [Fact]
    public void AStepWithNoCondition_OrAnUnknownMeasure_AlwaysApplies()
    {
        ApprovalStepCondition.Applies(Cond(ApprovalStepConditionMetric.None, ApprovalConditionOperator.GreaterOrEqual, null), new ApprovalRequestAttributes(Days: 1m))
            .Should().BeTrue();
        // A request filed before routing existed has no snapshot: asking for approval is the safe side.
        ApprovalStepCondition.Applies(Cond(ApprovalStepConditionMetric.Days, ApprovalConditionOperator.GreaterOrEqual, 5m), null).Should().BeTrue();
        ApprovalStepCondition.Applies(Cond(ApprovalStepConditionMetric.Hours, ApprovalConditionOperator.GreaterOrEqual, 5m), new ApprovalRequestAttributes(Days: 1m))
            .Should().BeTrue("hours aren't known for this request");
    }

    [Fact]
    public void EachRequestType_OffersOnlyTheMeasuresItHas()
    {
        ApprovalStepCondition.IsOfferedFor(ApprovalStepConditionMetric.Days, ApprovalApplicationType.Leave).Should().BeTrue();
        ApprovalStepCondition.IsOfferedFor(ApprovalStepConditionMetric.Hours, ApprovalApplicationType.Overtime).Should().BeTrue();
        ApprovalStepCondition.IsOfferedFor(ApprovalStepConditionMetric.Amount, ApprovalApplicationType.Loan).Should().BeTrue();
        ApprovalStepCondition.IsOfferedFor(ApprovalStepConditionMetric.Hours, ApprovalApplicationType.Leave).Should().BeFalse();
        ApprovalStepCondition.IsOfferedFor(ApprovalStepConditionMetric.Amount, ApprovalApplicationType.PassSlip).Should().BeFalse();
        ApprovalStepCondition.IsOfferedFor(ApprovalStepConditionMetric.Days, ApprovalApplicationType.Dtr).Should().BeFalse();
    }

    [Fact]
    public void ALeaveTypeScope_MatchesTheLeaveTypeFiled()
    {
        var sick = Guid.NewGuid();
        var vacation = Guid.NewGuid();
        var sickFlow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(), Name = "Sick", IsActive = true, ApplicationType = ApprovalApplicationType.Leave,
            Scopes = [new ApprovalWorkflowScope { Dimension = ApprovalScopeDimension.LeaveType, TargetId = sick }],
        };
        var defaultFlow = new ApprovalWorkflow { Id = Guid.NewGuid(), Name = "Default", IsActive = true, ApplicationType = ApprovalApplicationType.Leave };
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = Guid.NewGuid(), JobLevel = "", FirstName = "A", LastName = "B" };

        ApprovalScopeMatching.Select([sickFlow, defaultFlow], ApprovalScopeContext.FromEmployee(employee, new ApprovalRequestAttributes(LeaveTypeId: sick)))
            .Should().BeSameAs(sickFlow);
        ApprovalScopeMatching.Select([sickFlow, defaultFlow], ApprovalScopeContext.FromEmployee(employee, new ApprovalRequestAttributes(LeaveTypeId: vacation)))
            .Should().BeSameAs(defaultFlow);
        ApprovalScopeMatching.Select([sickFlow, defaultFlow], ApprovalScopeContext.FromEmployee(employee))
            .Should().BeSameAs(defaultFlow, "a request with no leave type doesn't match a leave-type scope");
    }

    // ---- engine on SQLite ----

    private readonly SqliteHrmsContext _db = new();
    private readonly Guid _groupId = Guid.NewGuid();
    private int _bio;
    private Employee _ana = null!, _ben = null!, _hana = null!;

    public ApprovalConditionalRoutingTests()
    {
        _ana = Make("Ana"); _ben = Make("Ben"); _hana = Make("Hana");
        using var seed = _db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = _groupId, Name = "Main" });
        seed.SaveChanges();
        seed.Employees.AddRange(_ana, _ben, _hana);
        seed.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private Employee Make(string first) => new()
    {
        Id = Guid.NewGuid(), PayrollGroupId = _groupId, JobLevel = "", FirstName = first, LastName = "X", BioId = ++_bio,
        EmployeeNo = $"E{_bio:000}", Email = $"{first.ToLower()}@acme.com", UserId = Guid.NewGuid(),
    };

    private async Task<ApprovalWorkflow> AddFlowAsync(string name, int priority, Guid? leaveType, params ApprovalWorkflowStep[] steps)
    {
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = name, IsActive = true, Priority = priority,
            Steps = steps.ToList(),
            Scopes = leaveType is { } t ? [new ApprovalWorkflowScope { Dimension = ApprovalScopeDimension.LeaveType, TargetId = t }] : [],
        };
        await using var seed = _db.NewContext();
        seed.ApprovalWorkflows.Add(workflow);
        await seed.SaveChangesAsync();
        return workflow;
    }

    private static ApprovalWorkflowStep PersonStep(int number, Guid person, ApprovalStepConditionMetric metric = ApprovalStepConditionMetric.None,
        ApprovalConditionOperator op = ApprovalConditionOperator.GreaterOrEqual, decimal? value = null) => new()
    {
        Id = Guid.NewGuid(), StepNumber = number, ApproverType = ApproverType.Person, ApproverEmployeeId = person, MinApprovals = 1,
        ConditionMetric = metric, ConditionOperator = op, ConditionValue = value,
    };

    private (ApprovalEngineService Engine, IPublishEndpoint Publisher, UnitOfWorkService Uow, HrmsContext Context) NewEngine()
    {
        var context = _db.NewContext();
        var uow = new UnitOfWorkService(context);
        var publisher = Substitute.For<IPublishEndpoint>();
        return (new ApprovalEngineService(uow, publisher), publisher, uow, context);
    }

    private async Task<Guid> FileAsync(ApprovalRequestAttributes attributes)
    {
        var applicationId = Guid.NewGuid();
        var (engine, _, uow, context) = NewEngine();
        await using var _ = context;
        await engine.StartAsync(ApprovalApplicationType.Leave, applicationId, _ana.Id, default, attributes: attributes);
        await uow.CommitChangesAsync(default);
        return applicationId;
    }

    private async Task<ApprovalActionResult> ApproveAsync(Guid applicationId, Employee approver)
    {
        var (engine, _, uow, context) = NewEngine();
        await using var _ = context;
        var result = await engine.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, _ana.Id, approver.Id, false, ApprovalActionType.Approved, null, default);
        await uow.CommitChangesAsync(default);
        return result;
    }

    private ApprovalWorkflowStep[] TwoStepFromFiveDays() =>
    [
        PersonStep(1, _ben.Id),
        PersonStep(2, _hana.Id, ApprovalStepConditionMetric.Days, ApprovalConditionOperator.GreaterOrEqual, 5m),
    ];

    [Fact]
    public async Task AShortLeave_SkipsTheStepThatOnlyAppliesFromFiveDays_AndIsApprovedAfterStepOne()
    {
        await AddFlowAsync("Leave", 100, null, TwoStepFromFiveDays());
        var applicationId = await FileAsync(new ApprovalRequestAttributes(Days: 2m));

        var result = await ApproveAsync(applicationId, _ben);

        result.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
        await using var read = _db.NewContext();
        var skipped = await read.ApprovalActions.AsNoTracking().SingleAsync(a => a.Action == ApprovalActionType.Skipped);
        skipped.StepNumber.Should().Be(2);
        skipped.ActorEmployeeId.Should().BeNull();
    }

    [Fact]
    public async Task ALongLeave_GoesOnToTheConditionalStep_AndItsApproverIsNotified()
    {
        await AddFlowAsync("Leave", 100, null, TwoStepFromFiveDays());
        var applicationId = await FileAsync(new ApprovalRequestAttributes(Days: 6m));

        var (engine, publisher, uow, context) = NewEngine();
        await using (context)
        {
            var first = await engine.RecordActionAsync(
                ApprovalApplicationType.Leave, applicationId, _ana.Id, _ben.Id, false, ApprovalActionType.Approved, null, default);
            await uow.CommitChangesAsync(default);
            first.InstanceStatus.Should().Be(ApprovalInstanceStatus.InProgress);
            first.CurrentStepNumber.Should().Be(2);
            await publisher.Received(1).Publish(
                Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "hana@acme.com"), Arg.Any<CancellationToken>());
        }

        (await ApproveAsync(applicationId, _hana)).InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task AWorkflowScopedToALeaveType_IsChosenForThatTypeOnly()
    {
        var sick = Guid.NewGuid();
        var sickFlow = await AddFlowAsync("Sick leave", 10, sick, PersonStep(1, _hana.Id));
        var defaultFlow = await AddFlowAsync("Leave", 100, null, PersonStep(1, _ben.Id));

        var sickRequest = await FileAsync(new ApprovalRequestAttributes(Days: 1m, LeaveTypeId: sick));
        var otherRequest = await FileAsync(new ApprovalRequestAttributes(Days: 1m, LeaveTypeId: Guid.NewGuid()));

        await using var read = _db.NewContext();
        (await read.ApprovalInstances.AsNoTracking().SingleAsync(i => i.ApplicationId == sickRequest)).ApprovalWorkflowId.Should().Be(sickFlow.Id);
        (await read.ApprovalInstances.AsNoTracking().SingleAsync(i => i.ApplicationId == otherRequest)).ApprovalWorkflowId.Should().Be(defaultFlow.Id);
    }

    [Fact]
    public async Task EditingARequestIntoAnotherLeaveType_MovesItToThatWorkflow_AndRestartsIt()
    {
        var sick = Guid.NewGuid();
        var sickFlow = await AddFlowAsync("Sick leave", 10, sick, PersonStep(1, _hana.Id));
        await AddFlowAsync("Leave", 100, null, PersonStep(1, _ben.Id));
        var applicationId = await FileAsync(new ApprovalRequestAttributes(Days: 1m, LeaveTypeId: Guid.NewGuid()));

        var (engine, _, uow, context) = NewEngine();
        await using (context)
        {
            var restarted = await engine.RestartIfProgressedAsync(
                ApprovalApplicationType.Leave, applicationId, default, new ApprovalRequestAttributes(Days: 1m, LeaveTypeId: sick));
            await uow.CommitChangesAsync(default);
            restarted.Should().BeTrue("nobody had approved, but the workflow changed");
        }

        await using var read = _db.NewContext();
        var instance = await read.ApprovalInstances.AsNoTracking().SingleAsync(i => i.ApplicationId == applicationId);
        instance.ApprovalWorkflowId.Should().Be(sickFlow.Id);
        instance.CycleNumber.Should().Be(2);
        instance.CurrentStepNumber.Should().Be(1);
        ApprovalRequestAttributes.FromJson(instance.AttributesJson)!.LeaveTypeId.Should().Be(sick);
    }

    [Fact]
    public async Task EditingTheDaysOfAnUntouchedRequest_RefreshesTheSnapshot_WithoutRestarting()
    {
        await AddFlowAsync("Leave", 100, null, TwoStepFromFiveDays());
        var applicationId = await FileAsync(new ApprovalRequestAttributes(Days: 2m));

        var (engine, _, uow, context) = NewEngine();
        await using (context)
        {
            (await engine.RestartIfProgressedAsync(ApprovalApplicationType.Leave, applicationId, default, new ApprovalRequestAttributes(Days: 8m)))
                .Should().BeFalse();
            await uow.CommitChangesAsync(default);
        }

        await using (var read = _db.NewContext())
        {
            var instance = await read.ApprovalInstances.AsNoTracking().SingleAsync();
            instance.CycleNumber.Should().Be(1);
            ApprovalRequestAttributes.FromJson(instance.AttributesJson)!.Days.Should().Be(8m);
        }

        // The longer leave now reaches the conditional step.
        (await ApproveAsync(applicationId, _ben)).CurrentStepNumber.Should().Be(2);
    }

    [Fact]
    public async Task EditingARequestAfterApproval_RestartsItFromTheFirstStep()
    {
        await AddFlowAsync("Leave", 100, null, TwoStepFromFiveDays());
        var applicationId = await FileAsync(new ApprovalRequestAttributes(Days: 6m));
        await ApproveAsync(applicationId, _ben);

        var (engine, _, uow, context) = NewEngine();
        await using (context)
        {
            (await engine.RestartIfProgressedAsync(ApprovalApplicationType.Leave, applicationId, default, new ApprovalRequestAttributes(Days: 7m)))
                .Should().BeTrue();
            await uow.CommitChangesAsync(default);
        }

        await using var read = _db.NewContext();
        var instance = await read.ApprovalInstances.AsNoTracking().SingleAsync();
        instance.CurrentStepNumber.Should().Be(1);
        instance.CycleNumber.Should().Be(2);
    }

    // ---- saving a workflow ----

    private async Task SaveAsync(ApprovalApplicationType type, params ApprovalWorkflowStep[] steps)
    {
        await using var context = _db.NewContext();
        await new ApprovalWorkflowService(new UnitOfWorkService(context)).CreateWorkflowAsync(
            new ApprovalWorkflow { Id = Guid.NewGuid(), ApplicationType = type, Name = "F", IsActive = false, Steps = steps.ToList() }, default);
    }

    [Fact]
    public async Task Saving_RejectsAConditionOnTheFirstStep_AMeasureTheTypeLacks_AndAMissingValue()
    {
        await FluentActions.Awaiting(() => SaveAsync(ApprovalApplicationType.Leave,
                PersonStep(1, _ben.Id, ApprovalStepConditionMetric.Days, ApprovalConditionOperator.GreaterOrEqual, 5m)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*first step*");
        await FluentActions.Awaiting(() => SaveAsync(ApprovalApplicationType.Leave,
                PersonStep(1, _ben.Id),
                PersonStep(2, _hana.Id, ApprovalStepConditionMetric.Hours, ApprovalConditionOperator.GreaterOrEqual, 2m)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*isn't something a Leave request has*");
        await FluentActions.Awaiting(() => SaveAsync(ApprovalApplicationType.Leave,
                PersonStep(1, _ben.Id),
                PersonStep(2, _hana.Id, ApprovalStepConditionMetric.Days, ApprovalConditionOperator.GreaterOrEqual, null)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*enter the amount*");
    }

    [Fact]
    public async Task Saving_AcceptsAConditionalLaterStep()
    {
        await SaveAsync(ApprovalApplicationType.Overtime,
            PersonStep(1, _ben.Id),
            PersonStep(2, _hana.Id, ApprovalStepConditionMetric.Hours, ApprovalConditionOperator.GreaterThan, 2m));

        await using var read = _db.NewContext();
        var step = await read.ApprovalWorkflowSteps.AsNoTracking().SingleAsync(s => s.StepNumber == 2);
        step.ConditionMetric.Should().Be(ApprovalStepConditionMetric.Hours);
        step.ConditionOperator.Should().Be(ApprovalConditionOperator.GreaterThan);
        step.ConditionValue.Should().Be(2m);
    }
}
