using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// My Approvals splits the requests still being approved: "For Approval" holds only the ones on the
/// caller's step right now; "In Progress" holds the rest they follow -- the type's Approve permission
/// (HR's overview), a step they already acted on, or a later step that will come to them. Both say which
/// step a request is on and who it waits for.
/// </summary>
public class MyApprovalsInProgressTests : IDisposable
{
    private readonly SqliteHrmsContext _db = new();
    private readonly Guid _groupId = Guid.NewGuid();
    private int _bioId;

    // Ana files; Ben approves step 1, Cara step 2; Dan covers for Cara; Eve is unrelated.
    private readonly Employee _ana, _ben, _cara, _dan, _eve;

    public MyApprovalsInProgressTests()
    {
        using var seed = _db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = _groupId, Name = "Main" });
        _ana = NewEmployee("Ana", "Reyes");
        _ben = NewEmployee("Ben", "Cruz");
        _cara = NewEmployee("Cara", "Lim");
        _dan = NewEmployee("Dan", "Yu");
        _eve = NewEmployee("Eve", "Ong");
        seed.Employees.AddRange(_ana, _ben, _cara, _dan, _eve);
        seed.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private Employee NewEmployee(string first, string last) => new()
    {
        Id = Guid.NewGuid(), PayrollGroupId = _groupId, JobLevel = "", FirstName = first, LastName = last,
        BioId = ++_bioId, EmployeeNo = $"E{_bioId:000}", Email = $"{first.ToLower()}@acme.com", UserId = Guid.NewGuid(),
    };

    private sealed class FakeAdapter : IApprovalRequestAdapter
    {
        public ApprovalApplicationType Type => ApprovalApplicationType.Leave;
        public string PermissionPrefix => "Leave";
        public Task<Dictionary<Guid, ApprovalRequestSummary>> DescribeAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) =>
            Task.FromResult(ids.ToDictionary(id => id, _ => new ApprovalRequestSummary("Vacation Leave · Oct 5", DateTime.UtcNow)));
        public Task ApproveAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) => Task.CompletedTask;
        public Task DeclineAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) => Task.CompletedTask;
    }

    private static ApprovalWorkflowStep PersonStep(int number, Employee approver) => new()
    {
        Id = Guid.NewGuid(), StepNumber = number, ApproverType = ApproverType.Person, ApproverEmployeeId = approver.Id, MinApprovals = 1,
    };

    private async Task<Guid> SeedTwoStepLeaveAsync()
    {
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = "Leave flow", IsActive = true,
            Steps = [PersonStep(1, _ben), PersonStep(2, _cara)],
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
        return applicationId;
    }

    private (ApprovalEngineService Engine, UnitOfWorkService Uow, HrmsContext Context) NewEngine()
    {
        var context = _db.NewContext();
        var uow = new UnitOfWorkService(context);
        return (new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>(), null, new ApprovalDelegationService(uow)), uow, context);
    }

    private async Task<List<MyApprovalItem>> PendingFor(Employee who, bool hasPermission = true)
    {
        var (engine, uow, context) = NewEngine();
        await using var _ = context;
        return await new MyApprovalsService(uow, engine, [new FakeAdapter()]).GetPendingAsync(who.Id, _ => hasPermission, default);
    }

    private async Task<List<MyApprovalItem>> InProgressFor(Employee who, bool hasPermission)
    {
        var (engine, uow, context) = NewEngine();
        await using var _ = context;
        return await new MyApprovalsService(uow, engine, [new FakeAdapter()]).GetInProgressAsync(who.Id, _ => hasPermission, default);
    }

    private async Task ApproveAsync(Guid applicationId, Employee who)
    {
        var (engine, uow, context) = NewEngine();
        await using var _ = context;
        await engine.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, _ana.Id, who.Id, callerHasOverrideAccess: false,
            ApprovalActionType.Approved, null, default);
        await uow.CommitChangesAsync(default);
    }

    [Fact]
    public async Task OnStepOne_OnlyTheStepOneApproverHasItForApproval_WithStepAndApprover()
    {
        await SeedTwoStepLeaveAsync();

        var ben = (await PendingFor(_ben)).Should().ContainSingle().Subject;
        ben.StepNumber.Should().Be(1);
        ben.TotalSteps.Should().Be(2);
        ben.CurrentApproverLabel.Should().Be("Ben Cruz");

        (await PendingFor(_cara)).Should().BeEmpty("step 2 isn't Cara's turn yet");
        var cara = (await InProgressFor(_cara, hasPermission: true)).Should().ContainSingle().Subject;
        cara.CurrentApproverLabel.Should().Be("Ben Cruz", "the in-progress row says who it is waiting on");
        (await InProgressFor(_ben, hasPermission: true)).Should().BeEmpty("it's on Ben's own For Approval list");
    }

    [Fact]
    public async Task AfterStepOneApproves_ItMovesToStepTwosForApproval_AndToStepOnesInProgress()
    {
        var applicationId = await SeedTwoStepLeaveAsync();
        await ApproveAsync(applicationId, _ben);

        var cara = (await PendingFor(_cara)).Should().ContainSingle().Subject;
        cara.StepNumber.Should().Be(2);
        cara.CurrentApproverLabel.Should().Be("Cara Lim");
        (await InProgressFor(_cara, hasPermission: true)).Should().BeEmpty();

        (await PendingFor(_ben)).Should().BeEmpty();
        (await InProgressFor(_ben, hasPermission: false)).Should().ContainSingle("Ben approved step 1 and follows it");
    }

    [Fact]
    public async Task ADelegateOfALaterStepsApprover_FollowsItWithoutThePermission()
    {
        await SeedTwoStepLeaveAsync();
        await using (var seed = _db.NewContext())
        {
            seed.ApprovalDelegations.Add(new ApprovalDelegation
            {
                Id = Guid.NewGuid(), DelegatorEmployeeId = _cara.Id, DelegateEmployeeId = _dan.Id, IsActive = true,
            });
            await seed.SaveChangesAsync();
        }

        (await PendingFor(_dan, hasPermission: false)).Should().BeEmpty();
        (await InProgressFor(_dan, hasPermission: false)).Should().ContainSingle();
    }

    [Fact]
    public async Task SomeoneUninvolvedWithoutThePermission_SeesNothing_ButThePermissionShowsEverything()
    {
        await SeedTwoStepLeaveAsync();

        (await PendingFor(_eve, hasPermission: false)).Should().BeEmpty();
        (await InProgressFor(_eve, hasPermission: false)).Should().BeEmpty();
        (await InProgressFor(_eve, hasPermission: true)).Should().ContainSingle("Approve permission gives HR the whole in-progress view");
    }

    [Fact]
    public async Task AReassignedStep_NamesTheReassignedApprover()
    {
        await SeedTwoStepLeaveAsync();
        await using (var edit = _db.NewContext())
            await edit.ApprovalInstances.ExecuteUpdateAsync(s => s.SetProperty(i => i.ReassignedApproverEmployeeId, _dan.Id));

        (await PendingFor(_ben)).Should().BeEmpty();
        (await PendingFor(_dan)).Should().ContainSingle().Which.CurrentApproverLabel.Should().Be("Dan Yu");
    }

    [Fact]
    public void Labels_ReadFromTheApplicantOrTheApproversSide()
    {
        var manager = new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.ApplicantManager, NamedApprovers = [] };
        var branchHead = new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.ApplicantBranchHead, NamedApprovers = [] };
        var names = new Dictionary<Guid, string>();
        var bossId = Guid.NewGuid();
        names[bossId] = "Ben Cruz";

        ApprovalStepLabeler.PoolLabel(manager, names, null).Should().Be("Your Manager");
        ApprovalStepLabeler.PoolLabel(manager, names, null, "Applicant").Should().Be("Applicant's Manager");
        ApprovalStepLabeler.PoolLabel(branchHead, names, new ResolvedApprovers([bossId], false), "Applicant")
            .Should().Be("Applicant's Branch Head: Ben Cruz");
        ApprovalStepLabeler.PoolLabel(branchHead, names, new ResolvedApprovers([], true))
            .Should().Be("Your Branch Head (no one assigned)");
    }
}
