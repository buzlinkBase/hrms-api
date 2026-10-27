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

namespace hrms.test.ServiceTests;

/// <summary>
/// Approval reminders and escalation: a step can remind its approvers after N hours (and repeat), and
/// escalate to a named person after M hours -- an addition to the approvers, not a replacement. Each
/// fires from when the request reached the step, and is recorded so a sweep never repeats itself.
/// </summary>
public class ApprovalSlaTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    // ---- pure timing rules ----

    private static ApprovalWorkflowStep Step(int? remind = null, int? repeat = null, int? escalate = null, Guid? escalateTo = null) => new()
    {
        StepNumber = 1, RemindAfterHours = remind, RemindRepeatHours = repeat, EscalateAfterHours = escalate, EscalateToEmployeeId = escalateTo, NamedApprovers = [],
    };

    private static ApprovalInstance Waiting(TimeSpan waited, int reminders = 0, TimeSpan? sinceLastReminder = null, Guid? escalatedTo = null) => new()
    {
        Id = Guid.NewGuid(), CurrentStepNumber = 1, CycleNumber = 1, CreatedAt = Now.AddDays(-30),
        StepEnteredAt = Now - waited, ReminderCount = reminders,
        LastReminderAt = sinceLastReminder.HasValue ? Now - sinceLastReminder.Value : null, EscalatedToEmployeeId = escalatedTo,
    };

    [Fact]
    public void ARemindersWaitsForItsFirstThreshold()
    {
        var step = Step(remind: 24);

        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromHours(23)), step, Now).Remind.Should().BeFalse();
        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromHours(24)), step, Now).Remind.Should().BeTrue();
        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromHours(100)), step, Now).Remind.Should().BeTrue();
    }

    [Fact]
    public void ARepeatComesOnlyAfterTheRepeatInterval_AndOnlyWhenOneIsSet()
    {
        var repeating = Step(remind: 24, repeat: 12);
        var once = Step(remind: 24);

        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromHours(40), 1, TimeSpan.FromHours(11)), repeating, Now).Remind.Should().BeFalse();
        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromHours(40), 1, TimeSpan.FromHours(12)), repeating, Now).Remind.Should().BeTrue();
        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromHours(400), 1, TimeSpan.FromHours(300)), once, Now).Remind.Should().BeFalse("no repeat configured");
    }

    [Fact]
    public void RemindersStopAtTheCap()
    {
        var step = Step(remind: 1, repeat: 1);

        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromHours(500), ApprovalSlaPolicy.MaxReminders - 1, TimeSpan.FromHours(5)), step, Now).Remind.Should().BeTrue();
        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromHours(500), ApprovalSlaPolicy.MaxReminders, TimeSpan.FromHours(5)), step, Now).Remind.Should().BeFalse();
    }

    [Fact]
    public void EscalationHappensOnce_AfterItsThreshold_AndNeedsAPerson()
    {
        var someone = Guid.NewGuid();
        var step = Step(escalate: 48, escalateTo: someone);

        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromHours(47)), step, Now).Escalate.Should().BeFalse();
        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromHours(48)), step, Now).Escalate.Should().BeTrue();
        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromHours(90), escalatedTo: someone), step, Now).Escalate.Should().BeFalse("already escalated");
        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromHours(90)), Step(escalate: 48), Now).Escalate.Should().BeFalse("nobody to escalate to");
    }

    [Fact]
    public void AStepWithNoTiming_NeverDoesAnything()
    {
        ApprovalSlaPolicy.HasSla(Step()).Should().BeFalse();
        ApprovalSlaPolicy.HasSla(Step(remind: 1)).Should().BeTrue();
        ApprovalSlaPolicy.HasSla(Step(escalate: 5)).Should().BeFalse("an escalation with no person is nothing");
        ApprovalSlaPolicy.HasSla(Step(escalate: 5, escalateTo: Guid.NewGuid())).Should().BeTrue();
        ApprovalSlaPolicy.Evaluate(Waiting(TimeSpan.FromDays(30)), Step(), Now).Any.Should().BeFalse();
    }

    [Fact]
    public void WaitingIsCountedFromWhenTheStepWasEntered_OrTheLastApproval_OrFiling()
    {
        var filed = Now.AddDays(-10);
        var approvedAt = Now.AddDays(-3);

        var noTimer = new ApprovalInstance
        {
            CurrentStepNumber = 2, CycleNumber = 1, CreatedAt = filed,
            Actions = [new ApprovalAction { StepNumber = 1, CycleNumber = 1, Action = ApprovalActionType.Approved, CreatedAt = approvedAt }],
        };
        ApprovalSlaPolicy.StepEnteredAt(noTimer).Should().Be(approvedAt);

        ApprovalSlaPolicy.StepEnteredAt(new ApprovalInstance { CurrentStepNumber = 1, CycleNumber = 1, CreatedAt = filed }).Should().Be(filed);
        ApprovalSlaPolicy.StepEnteredAt(new ApprovalInstance { StepEnteredAt = Now.AddHours(-5), CreatedAt = filed }).Should().Be(Now.AddHours(-5));
    }

    [Fact]
    public void DescribeWait_ReadsNaturally()
    {
        ApprovalSlaPolicy.DescribeWait(TimeSpan.FromHours(5)).Should().Be("5 hours");
        ApprovalSlaPolicy.DescribeWait(TimeSpan.FromMinutes(30)).Should().Be("1 hour");
        ApprovalSlaPolicy.DescribeWait(TimeSpan.FromHours(24)).Should().Be("1 day");
        ApprovalSlaPolicy.DescribeWait(TimeSpan.FromHours(52)).Should().Be("2 days 4 hours");
    }

    // ---- the sweep, escalation and access, on SQLite ----

    private readonly SqliteHrmsContext _db = new();
    private readonly Guid _groupId = Guid.NewGuid();
    private int _bio;
    private Employee _ana = null!, _ben = null!, _cara = null!;

    public ApprovalSlaTests()
    {
        _ana = Make("Ana"); _ben = Make("Ben"); _cara = Make("Cara");
        using var seed = _db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = _groupId, Name = "Main" });
        seed.SaveChanges();
        seed.Employees.AddRange(_ana, _ben, _cara);
        seed.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private Employee Make(string first) => new()
    {
        Id = Guid.NewGuid(), PayrollGroupId = _groupId, JobLevel = "", FirstName = first, LastName = "X", BioId = ++_bio,
        EmployeeNo = $"E{_bio:000}", Email = $"{first.ToLower()}@acme.com", UserId = Guid.NewGuid(),
    };

    private async Task<Guid> SeedWaitingAsync(TimeSpan waited, int? remind = null, int? repeat = null, int? escalate = null,
        Guid? escalateTo = null, bool twoSteps = false)
    {
        var steps = new List<ApprovalWorkflowStep>
        {
            new()
            {
                Id = Guid.NewGuid(), StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = _ben.Id, MinApprovals = 1,
                RemindAfterHours = remind, RemindRepeatHours = repeat, EscalateAfterHours = escalate, EscalateToEmployeeId = escalateTo,
            },
        };
        if (twoSteps)
            steps.Add(new ApprovalWorkflowStep { Id = Guid.NewGuid(), StepNumber = 2, ApproverType = ApproverType.Person, ApproverEmployeeId = _cara.Id, MinApprovals = 1 });
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = "Flow", IsActive = true, Steps = steps,
        };
        var applicationId = Guid.NewGuid();
        await using var seed = _db.NewContext();
        seed.ApprovalWorkflows.Add(workflow);
        seed.ApprovalInstances.Add(new ApprovalInstance
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, ApplicationId = applicationId,
            ApplicantEmployeeId = _ana.Id, ApprovalWorkflowId = workflow.Id, CurrentStepNumber = 1,
            StepEnteredAt = DateTime.UtcNow - waited,
        });
        await seed.SaveChangesAsync();
        return applicationId;
    }

    private (ApprovalSlaService Service, ApprovalEngineService Engine, IPublishEndpoint Publisher, UnitOfWorkService Uow, HrmsContext Context) NewSla()
    {
        var context = _db.NewContext();
        var uow = new UnitOfWorkService(context);
        var publisher = Substitute.For<IPublishEndpoint>();
        var engine = new ApprovalEngineService(uow, publisher);
        return (new ApprovalSlaService(uow, engine), engine, publisher, uow, context);
    }

    private async Task<(SlaSweepResult Result, IPublishEndpoint Publisher)> SweepAsync()
    {
        var (service, _, publisher, _, context) = NewSla();
        await using var _ = context;
        return (await service.RunAsync(DateTime.UtcNow, default), publisher);
    }

    [Fact]
    public async Task TheSweep_RemindsAWaitingApprover_Once_AndRecordsIt()
    {
        var applicationId = await SeedWaitingAsync(TimeSpan.FromHours(30), remind: 24);

        var (first, publisher) = await SweepAsync();

        first.Should().Be(new SlaSweepResult(1, 0));
        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.RecipientEmail == "ben@acme.com" && m.StatusLabel == ApprovalEngineService.PendingApprovalStatusLabel &&
                m.Note != null && m.Note.StartsWith("Reminder: waiting 1 day")),
            Arg.Any<CancellationToken>());
        await using (var read = _db.NewContext())
        {
            var instance = await read.ApprovalInstances.AsNoTracking().SingleAsync(i => i.ApplicationId == applicationId);
            instance.ReminderCount.Should().Be(1);
            instance.LastReminderAt.Should().NotBeNull();
        }

        // Sweeping again straight away does nothing: the reminder was recorded.
        (await SweepAsync()).Result.Total.Should().Be(0);
    }

    [Fact]
    public async Task TheSweep_LeavesAlone_AStepWithNoTiming_OrOneNotWaitingLongEnough()
    {
        await SeedWaitingAsync(TimeSpan.FromDays(30));
        await SeedWaitingAsync(TimeSpan.FromHours(2), remind: 24);

        (await SweepAsync()).Result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Escalation_OpensTheStepToTheNamedPerson_AddsATimelineEntry_AndTellsThem()
    {
        var applicationId = await SeedWaitingAsync(TimeSpan.FromHours(60), escalate: 48, escalateTo: _cara.Id);

        var (result, publisher) = await SweepAsync();

        result.Should().Be(new SlaSweepResult(0, 1));
        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "cara@acme.com" && m.Note != null && m.Note.StartsWith("Escalated")),
            Arg.Any<CancellationToken>());
        await using var read = _db.NewContext();
        (await read.ApprovalInstances.AsNoTracking().SingleAsync(i => i.ApplicationId == applicationId)).EscalatedToEmployeeId.Should().Be(_cara.Id);
        var entry = await read.ApprovalActions.AsNoTracking().SingleAsync(a => a.Action == ApprovalActionType.Escalated);
        entry.ActorEmployeeId.Should().BeNull();
        entry.Note.Should().Contain("Cara");

        (await SweepAsync()).Result.Total.Should().Be(0);
    }

    [Fact]
    public async Task TheEscalatedPerson_CanApprove_WhileTheOriginalApproverStillCan()
    {
        var applicationId = await SeedWaitingAsync(TimeSpan.FromHours(60), escalate: 48, escalateTo: _cara.Id);
        await SweepAsync();

        // Fresh engine over its own context for each question.
        async Task<bool> Eligible(Employee who)
        {
            var (_, e, _, _, c) = NewSla();
            await using var _ = c;
            return await e.IsCallerEligibleAsync(ApprovalApplicationType.Leave, applicationId, who.Id);
        }

        (await Eligible(_cara)).Should().BeTrue();
        (await Eligible(_ben)).Should().BeTrue("escalation adds to the approvers, it doesn't replace them");
        var stranger = Make("Dan");
        await using (var seed = _db.NewContext()) { seed.Employees.Add(stranger); await seed.SaveChangesAsync(); }
        (await Eligible(stranger)).Should().BeFalse();
    }

    [Fact]
    public async Task EscalatedPersonApproving_ClearsTheEscalation_AndGrantsAccessWithoutThePermission()
    {
        var applicationId = await SeedWaitingAsync(TimeSpan.FromHours(60), escalate: 48, escalateTo: _cara.Id);
        await SweepAsync();

        var (_, engine, _, uow, context) = NewSla();
        await using (context)
        {
            var result = await engine.RecordActionAsync(
                ApprovalApplicationType.Leave, applicationId, _ana.Id, _cara.Id, callerHasOverrideAccess: false,
                ApprovalActionType.Approved, null, default);
            await uow.CommitChangesAsync("", default);
            result.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
        }

        await using var read = _db.NewContext();
        (await read.ApprovalInstances.AsNoTracking().SingleAsync()).EscalatedToEmployeeId.Should().BeNull();

        // The standing the engine reports for escalation waives the Approve permission (like a delegation).
        ApprovalStanding.Escalated.GrantsAccess.Should().BeTrue();
        ApprovalStanding.Direct.GrantsAccess.Should().BeFalse();
    }

    [Fact]
    public async Task MyApprovals_ListsTheRequestForTheEscalatedPerson_EvenWithoutTheApprovePermission()
    {
        await SeedWaitingAsync(TimeSpan.FromHours(60), escalate: 48, escalateTo: _cara.Id);
        await SweepAsync();

        var (_, engine, _, uow, context) = NewSla();
        await using (context)
        {
            var service = new MyApprovalsService(uow, engine, [new StubAdapter()]);
            (await service.GetPendingAsync(_cara.Id, _ => false, default)).Should().ContainSingle();
            (await service.GetPendingAsync(_ben.Id, _ => false, default)).Should().BeEmpty("Ben is a direct approver and has no permission");
        }
    }

    private sealed class StubAdapter : IApprovalRequestAdapter
    {
        public ApprovalApplicationType Type => ApprovalApplicationType.Leave;
        public string PermissionPrefix => "Leave";
        public Task<Dictionary<Guid, ApprovalRequestSummary>> DescribeAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) =>
            Task.FromResult(ids.ToDictionary(id => id, _ => new ApprovalRequestSummary("Leave", DateTime.UtcNow)));
        public Task ApproveAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) => Task.CompletedTask;
        public Task DeclineAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) => Task.CompletedTask;
    }

    [Fact]
    public async Task WhenTheStepAdvances_TheClockAndTheEscalationStartOver()
    {
        var applicationId = await SeedWaitingAsync(TimeSpan.FromHours(60), remind: 24, escalate: 48, escalateTo: _cara.Id, twoSteps: true);
        await SweepAsync();

        var (_, engine, _, uow, context) = NewSla();
        await using (context)
        {
            var result = await engine.RecordActionAsync(
                ApprovalApplicationType.Leave, applicationId, _ana.Id, _ben.Id, false, ApprovalActionType.Approved, null, default);
            await uow.CommitChangesAsync("", default);
            result.CurrentStepNumber.Should().Be(2);
        }

        await using var read = _db.NewContext();
        var instance = await read.ApprovalInstances.AsNoTracking().SingleAsync();
        instance.EscalatedToEmployeeId.Should().BeNull();
        instance.ReminderCount.Should().Be(0);
        instance.LastReminderAt.Should().BeNull();
        instance.StepEnteredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    // ---- saving timing ----

    private async Task SaveAsync(ApprovalWorkflowStep step)
    {
        await using var context = _db.NewContext();
        step.ApproverType = ApproverType.Person;
        step.ApproverEmployeeId = _ben.Id;
        step.StepNumber = 1;
        await new ApprovalWorkflowService(new UnitOfWorkService(context)).CreateWorkflowAsync(
            new ApprovalWorkflow { Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = "F", IsActive = false, Steps = [step] }, default);
    }

    [Fact]
    public async Task Saving_RejectsBadTiming_AnEscalationWithoutAPerson_AndARepeatWithoutAFirstReminder()
    {
        await FluentActions.Awaiting(() => SaveAsync(Step(remind: 0))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*1 to*hours*");
        await FluentActions.Awaiting(() => SaveAsync(Step(repeat: 6))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*before repeating*");
        await FluentActions.Awaiting(() => SaveAsync(Step(escalate: 24))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*person to escalate to*");
        await FluentActions.Awaiting(() => SaveAsync(Step(escalateTo: _cara.Id))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*how long to wait*");
    }

    [Fact]
    public async Task Saving_AcceptsReminderAndEscalationTiming()
    {
        await SaveAsync(Step(remind: 24, repeat: 12, escalate: 72, escalateTo: _cara.Id));

        await using var read = _db.NewContext();
        var saved = await read.ApprovalWorkflowSteps.AsNoTracking().SingleAsync();
        (saved.RemindAfterHours, saved.RemindRepeatHours, saved.EscalateAfterHours, saved.EscalateToEmployeeId)
            .Should().Be((24, 12, 72, _cara.Id));
    }
}
