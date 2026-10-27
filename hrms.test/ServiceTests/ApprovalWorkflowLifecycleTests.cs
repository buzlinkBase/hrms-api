using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Onepunch.Common.Lib.Exceptions;

namespace hrms.test.ServiceTests;

/// <summary>
/// When an approval workflow may change: never while a request is still running on it (those
/// read its steps live), freely once they've all finished; deletion only if never used. And the
/// scheduled switch: a new version takes over from the active one as soon as it has drained.
/// </summary>
public class ApprovalWorkflowLifecycleTests : IDisposable
{
    private readonly SqliteHrmsContext _db = new();
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _leaveTypeId = Guid.NewGuid();

    public ApprovalWorkflowLifecycleTests()
    {
        using var seed = _db.NewContext();
        var group = new PayrollGroup { Id = Guid.NewGuid(), Name = "Main" };
        seed.PayrollGroups.Add(group);
        seed.Employees.Add(new Employee
        {
            Id = _employeeId, PayrollGroupId = group.Id, JobLevel = "", FirstName = "Ana", LastName = "Reyes", BioId = 1,
        });
        seed.Leaves.Add(new Leave { Id = _leaveTypeId, Code = "VL", Description = "Vacation Leave" });
        seed.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private static ApprovalWorkflow Workflow(string name, bool active) => new()
    {
        Id = Guid.NewGuid(),
        ApplicationType = ApprovalApplicationType.Leave,
        Name = name,
        IsActive = active,
        Steps =
        [
            new ApprovalWorkflowStep { Id = Guid.NewGuid(), StepNumber = 1, ApproverType = ApproverType.ApplicantManager },
        ],
    };

    private async Task<ApprovalWorkflow> AddWorkflowAsync(string name, bool active)
    {
        var workflow = Workflow(name, active);
        await using var seed = _db.NewContext();
        seed.ApprovalWorkflows.Add(workflow);
        await seed.SaveChangesAsync();
        return workflow;
    }

    /// <summary>A pending leave application with its request on <paramref name="workflowId"/>.</summary>
    private async Task<Guid> FileLeaveAsync(Guid workflowId, ApprovalInstanceStatus status = ApprovalInstanceStatus.InProgress,
        ApprovalStatus applicationStatus = ApprovalStatus.ForApproval)
    {
        var application = new LeaveApplication
        {
            Id = Guid.NewGuid(), EmployeeId = _employeeId, LeaveId = _leaveTypeId, Leave = null!,
            DurationType = DurationType.SingleDay, LeaveDateFrom = new DateOnly(2026, 10, 5), LeaveDateTo = new DateOnly(2026, 10, 5),
            ApprovalStatus = applicationStatus,
        };
        await using var seed = _db.NewContext();
        seed.leaveApplications.Add(application);
        seed.ApprovalInstances.Add(new ApprovalInstance
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, ApplicationId = application.Id,
            ApplicantEmployeeId = _employeeId, ApprovalWorkflowId = workflowId, CurrentStepNumber = 1, Status = status,
        });
        await seed.SaveChangesAsync();
        return application.Id;
    }

    /// <summary>One request's worth of work, in its own scope (a unit of work holds a write lock while open).</summary>
    private async Task WithServiceAsync(Func<ApprovalWorkflowService, Task> act)
    {
        await using var context = _db.NewContext();
        await act(new ApprovalWorkflowService(new UnitOfWorkService(context)));
    }

    private async Task<ApprovalWorkflow> ReloadAsync(Guid id)
    {
        await using var read = _db.NewContext();
        return await read.ApprovalWorkflows.AsNoTracking().SingleAsync(w => w.Id == id);
    }

    private static ApprovalWorkflow Edited(string name) => new()
    {
        Name = name,
        Steps =
        [
            new ApprovalWorkflowStep { StepNumber = 1, ApproverType = ApproverType.ApplicantManager },
            new ApprovalWorkflowStep { StepNumber = 2, ApproverType = ApproverType.ApplicantDepartment },
        ],
    };

    [Fact]
    public async Task Editing_IsRefused_WhileARequestIsInProgress_AndAllowed_OnceItHasFinished()
    {
        var workflow = await AddWorkflowAsync("Leave", active: true);
        await FileLeaveAsync(workflow.Id);

        var whileRunning = () => WithServiceAsync(s => s.UpdateWorkflowAsync(workflow.Id, Edited("Leave v2"), default));
        (await whileRunning.Should().ThrowAsync<GuardException>()).WithMessage("1 request is still going through*");

        await using (var finish = _db.NewContext())
        {
            await finish.ApprovalInstances.ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, ApprovalInstanceStatus.Approved));
        }

        await WithServiceAsync(s => s.UpdateWorkflowAsync(workflow.Id, Edited("Leave v2"), default));
        await using var read = _db.NewContext();
        (await read.ApprovalWorkflowSteps.CountAsync(st => st.ApprovalWorkflowId == workflow.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Deleting_IsOnlyAllowed_ForAWorkflowNoRequestHasEverUsed()
    {
        var used = await AddWorkflowAsync("Used", active: false);
        await FileLeaveAsync(used.Id, ApprovalInstanceStatus.Approved, ApprovalStatus.Approved);
        var unused = await AddWorkflowAsync("Unused", active: false);

        var deleteUsed = () => WithServiceAsync(s => s.DeleteAsync(used.Id, default));
        (await deleteUsed.Should().ThrowAsync<GuardException>()).WithMessage("*Deactivate it instead*");

        await WithServiceAsync(s => s.DeleteAsync(unused.Id, default));
        await using var read = _db.NewContext();
        (await read.ApprovalWorkflows.AnyAsync(w => w.Id == unused.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Usage_CountsWhatIsStillRunning_AndWhetherItWasEverUsed()
    {
        var workflow = await AddWorkflowAsync("Leave", active: true);
        await FileLeaveAsync(workflow.Id);
        await FileLeaveAsync(workflow.Id, ApprovalInstanceStatus.Declined, ApprovalStatus.Declined);
        var fresh = await AddWorkflowAsync("Fresh", active: false);

        Dictionary<Guid, WorkflowUsage> usage = null!;
        await WithServiceAsync(async s => usage = await s.GetUsageAsync([workflow.Id, fresh.Id], default));

        usage[workflow.Id].Should().Be(new WorkflowUsage(1, true));
        usage[workflow.Id].CanEdit.Should().BeFalse();
        usage[fresh.Id].Should().Be(WorkflowUsage.None);
        usage[fresh.Id].CanDelete.Should().BeTrue();
    }

    [Fact]
    public async Task Scheduling_WithNothingPending_SwitchesRightAway()
    {
        var current = await AddWorkflowAsync("Leave", active: true);
        var next = await AddWorkflowAsync("Leave v2", active: false);

        var activated = false;
        await WithServiceAsync(async s => activated = await s.ScheduleActivationAsync(next.Id, default));

        activated.Should().BeTrue();
        (await ReloadAsync(next.Id)).IsActive.Should().BeTrue();
        (await ReloadAsync(current.Id)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AScheduledVersion_WaitsForPendingRequests_ThenTakesOverWhenTheLastIsWithdrawn()
    {
        var current = await AddWorkflowAsync("Leave", active: true);
        var next = await AddWorkflowAsync("Leave v2", active: false);
        var firstLeave = await FileLeaveAsync(current.Id);
        var secondLeave = await FileLeaveAsync(current.Id);

        await WithServiceAsync(s => s.ScheduleActivationAsync(next.Id, default));
        (await ReloadAsync(next.Id)).Should().Match<ApprovalWorkflow>(w => !w.IsActive && w.ReplacesWorkflowId == current.Id);
        (await ReloadAsync(current.Id)).IsActive.Should().BeTrue("new requests keep using it until it drains");

        await CancelRequestAsync(firstLeave);
        (await ReloadAsync(next.Id)).IsActive.Should().BeFalse("one request is still pending");

        await CancelRequestAsync(secondLeave);
        (await ReloadAsync(next.Id)).Should().Match<ApprovalWorkflow>(w => w.IsActive && w.ReplacesWorkflowId == null);
        (await ReloadAsync(current.Id)).IsActive.Should().BeFalse();
    }

    // What every withdraw does: the engine closes the request and the caller saves.
    private async Task CancelRequestAsync(Guid leaveId)
    {
        await using var context = _db.NewContext();
        var uow = new UnitOfWorkService(context);
        await new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>())
            .CancelAsync(ApprovalApplicationType.Leave, leaveId, default);
        await uow.CommitChangesAsync("", default);
    }

    [Fact]
    public async Task Withdrawing_ClosesTheRequest_SoTheWorkflowCanChange()
    {
        var workflow = await AddWorkflowAsync("Leave", active: true);
        var leave = await FileLeaveAsync(workflow.Id);

        await CancelRequestAsync(leave);

        await using var read = _db.NewContext();
        (await read.ApprovalInstances.SingleAsync()).Status.Should().Be(ApprovalInstanceStatus.Cancelled);
        await WithServiceAsync(s => s.UpdateWorkflowAsync(workflow.Id, Edited("Leave v2"), default));
    }

    [Fact]
    public async Task OpeningTheList_ClosesRequestsLeftPendingByAnOldWithdrawal_AndCompletesTheSwitch()
    {
        var current = await AddWorkflowAsync("Leave", active: true);
        var next = await AddWorkflowAsync("Leave v2", active: false);
        // Withdrawn before withdrawals closed their request: the request was left "in progress".
        await FileLeaveAsync(current.Id, ApprovalInstanceStatus.InProgress, ApprovalStatus.Withdrawn);
        await using (var schedule = _db.NewContext())
        {
            await schedule.ApprovalWorkflows.Where(w => w.Id == next.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.ReplacesWorkflowId, current.Id));
        }

        await WithServiceAsync(s => s.ReconcileAsync(ApprovalApplicationType.Leave, default));

        await using var read = _db.NewContext();
        (await read.ApprovalInstances.SingleAsync()).Status.Should().Be(ApprovalInstanceStatus.Cancelled);
        (await ReloadAsync(next.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ASchedule_IsDropped_WhenTheNewVersionIsEdited_OrTheCurrentOneDeactivated()
    {
        var current = await AddWorkflowAsync("Leave", active: true);
        await FileLeaveAsync(current.Id);
        var next = await AddWorkflowAsync("Leave v2", active: false);

        await WithServiceAsync(s => s.ScheduleActivationAsync(next.Id, default));
        await WithServiceAsync(s => s.UpdateWorkflowAsync(next.Id, Edited("Leave v2b"), default));
        (await ReloadAsync(next.Id)).ReplacesWorkflowId.Should().BeNull();

        await WithServiceAsync(s => s.ScheduleActivationAsync(next.Id, default));
        await WithServiceAsync(s => s.DeactivateAsync(current.Id, default));
        (await ReloadAsync(next.Id)).ReplacesWorkflowId.Should().BeNull();
    }

    [Fact]
    public async Task ActivatingNow_SwitchesImmediately_EvenWithRequestsPending()
    {
        var current = await AddWorkflowAsync("Leave", active: true);
        await FileLeaveAsync(current.Id);
        var next = await AddWorkflowAsync("Leave v2", active: false);

        await WithServiceAsync(s => s.ActivateAsync(next.Id, default));

        (await ReloadAsync(next.Id)).IsActive.Should().BeTrue();
        (await ReloadAsync(current.Id)).IsActive.Should().BeFalse("its pending request finishes on it, new ones don't start there");
    }

    // ---- several active workflows per type, one per scope slot ----

    private static readonly Guid BranchA = Guid.NewGuid();
    private static readonly Guid BranchB = Guid.NewGuid();

    private async Task<ApprovalWorkflow> AddScopedAsync(string name, bool active, int priority, params (ApprovalScopeDimension Dimension, Guid Id)[] conditions)
    {
        var workflow = Workflow(name, active);
        workflow.Priority = priority;
        workflow.Scopes = conditions.Select(c => new ApprovalWorkflowScope
        {
            Id = Guid.NewGuid(), ApprovalWorkflowId = workflow.Id, Dimension = c.Dimension, TargetId = c.Id,
        }).ToList();
        await using var seed = _db.NewContext();
        seed.ApprovalWorkflows.Add(workflow);
        await seed.SaveChangesAsync();
        return workflow;
    }

    private static ApprovalWorkflow ScopedEdit(string name, int priority, params (ApprovalScopeDimension Dimension, Guid Id)[] conditions)
    {
        var edited = Edited(name);
        edited.Priority = priority;
        edited.Scopes = conditions.Select(c => new ApprovalWorkflowScope { Dimension = c.Dimension, TargetId = c.Id }).ToList();
        return edited;
    }

    [Fact]
    public async Task WorkflowsInDifferentSlots_StayActiveTogether()
    {
        var everyone = await AddScopedAsync("Default", active: true, 100);
        var branchA = await AddScopedAsync("Branch A", active: false, 10, (ApprovalScopeDimension.Branch, BranchA));
        var branchB = await AddScopedAsync("Branch B", active: false, 10, (ApprovalScopeDimension.Branch, BranchB));

        await WithServiceAsync(s => s.ActivateAsync(branchA.Id, default));
        await WithServiceAsync(s => s.ActivateAsync(branchB.Id, default));

        (await ReloadAsync(everyone.Id)).IsActive.Should().BeTrue();
        (await ReloadAsync(branchA.Id)).IsActive.Should().BeTrue();
        (await ReloadAsync(branchB.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Activating_ANewVersion_ReplacesOnlyTheOneInItsOwnSlot()
    {
        var branchA = await AddScopedAsync("Branch A", active: true, 10, (ApprovalScopeDimension.Branch, BranchA));
        var branchB = await AddScopedAsync("Branch B", active: true, 10, (ApprovalScopeDimension.Branch, BranchB));
        var branchAv2 = await AddScopedAsync("Branch A v2", active: false, 10, (ApprovalScopeDimension.Branch, BranchA));

        await WithServiceAsync(s => s.ActivateAsync(branchAv2.Id, default));

        (await ReloadAsync(branchAv2.Id)).IsActive.Should().BeTrue();
        (await ReloadAsync(branchA.Id)).IsActive.Should().BeFalse();
        (await ReloadAsync(branchB.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ScheduleActivation_WaitsOnTheWorkflowInTheSameSlot_NotOnOthers()
    {
        var branchA = await AddScopedAsync("Branch A", active: true, 10, (ApprovalScopeDimension.Branch, BranchA));
        var branchB = await AddScopedAsync("Branch B", active: true, 10, (ApprovalScopeDimension.Branch, BranchB));
        await FileLeaveAsync(branchB.Id); // busy, but in another slot
        var branchAv2 = await AddScopedAsync("Branch A v2", active: false, 10, (ApprovalScopeDimension.Branch, BranchA));

        var activatedAtOnce = false;
        await WithServiceAsync(async s => activatedAtOnce = await s.ScheduleActivationAsync(branchAv2.Id, default));

        activatedAtOnce.Should().BeTrue("Branch A has nothing in progress, whatever Branch B is doing");
        (await ReloadAsync(branchA.Id)).IsActive.Should().BeFalse();
        (await ReloadAsync(branchB.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task EditingAnActiveWorkflow_IntoAnotherActiveOnesSlot_IsRefused()
    {
        var branchA = await AddScopedAsync("Branch A", active: true, 10, (ApprovalScopeDimension.Branch, BranchA));
        await AddScopedAsync("Branch B", active: true, 10, (ApprovalScopeDimension.Branch, BranchB));

        var clash = () => WithServiceAsync(s => s.UpdateWorkflowAsync(
            branchA.Id, ScopedEdit("Branch A", 10, (ApprovalScopeDimension.Branch, BranchB)), default));

        (await clash.Should().ThrowAsync<GuardException>()).WithMessage("Another active workflow already applies*");
    }

    [Fact]
    public async Task EditingScopeAndPriority_ReplacesTheConditions()
    {
        var workflow = await AddScopedAsync("Branch A", active: false, 10, (ApprovalScopeDimension.Branch, BranchA));

        await WithServiceAsync(s => s.UpdateWorkflowAsync(
            workflow.Id, ScopedEdit("Branch B", 20, (ApprovalScopeDimension.Branch, BranchB), (ApprovalScopeDimension.Department, Guid.NewGuid())), default));

        await using var read = _db.NewContext();
        var saved = await read.ApprovalWorkflows.Include(w => w.Scopes).AsNoTracking().SingleAsync(w => w.Id == workflow.Id);
        saved.Priority.Should().Be(20);
        saved.Scopes.Should().HaveCount(2);
        saved.Scopes.Should().ContainSingle(c => c.Dimension == ApprovalScopeDimension.Branch && c.TargetId == BranchB);
    }

    [Fact]
    public async Task Saving_RejectsBadPriorityAndBadConditions()
    {
        var badPriority = () => WithServiceAsync(s => s.CreateWorkflowAsync(
            ScopedWorkflow(priority: 0), default));
        await badPriority.Should().ThrowAsync<InvalidOperationException>().WithMessage("Priority must be between*");

        var noTarget = () => WithServiceAsync(s => s.CreateWorkflowAsync(
            ScopedWorkflow(priority: 10, new ApprovalWorkflowScope { Dimension = ApprovalScopeDimension.Branch }), default));
        await noTarget.Should().ThrowAsync<InvalidOperationException>();

        var badStatus = () => WithServiceAsync(s => s.CreateWorkflowAsync(
            ScopedWorkflow(priority: 10, new ApprovalWorkflowScope { Dimension = ApprovalScopeDimension.EmploymentStatus, TargetValue = "Wizard" }), default));
        await badStatus.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not an employment status*");
    }

    private static ApprovalWorkflow ScopedWorkflow(int priority, params ApprovalWorkflowScope[] scopes)
    {
        var workflow = Workflow("Scoped", active: false);
        workflow.Priority = priority;
        workflow.Scopes = scopes.ToList();
        return workflow;
    }

    [Fact]
    public async Task ResolveForEmployee_PicksTheBestActiveMatch_AndListsTheCandidates()
    {
        Guid groupId;
        await using (var read = _db.NewContext())
            groupId = (await read.Employees.AsNoTracking().SingleAsync(e => e.Id == _employeeId)).PayrollGroupId;
        var everyone = await AddScopedAsync("Default", active: true, 100);
        var mine = await AddScopedAsync("My payroll group", active: true, 10, (ApprovalScopeDimension.PayrollGroup, groupId));
        await AddScopedAsync("Someone else's branch", active: true, 5, (ApprovalScopeDimension.Branch, BranchA));
        await AddScopedAsync("Inactive", active: false, 1);

        WorkflowResolution? resolution = null;
        await WithServiceAsync(async s => resolution = await s.ResolveForEmployeeAsync(ApprovalApplicationType.Leave, _employeeId, default));

        resolution!.Winner!.Id.Should().Be(mine.Id);
        resolution.Candidates.Select(c => (c.Workflow.Name, c.Matched)).Should().Equal(
            ("Someone else's branch", false), ("My payroll group", true), ("Default", true));
        resolution.Candidates.Should().NotContain(c => c.Workflow.Id == everyone.Id && !c.Matched);
    }

    [Fact]
    public async Task ResolveForEmployee_WithNothingMatching_IsTheFallback()
    {
        await AddScopedAsync("Branch A only", active: true, 10, (ApprovalScopeDimension.Branch, BranchA));

        WorkflowResolution? resolution = null;
        await WithServiceAsync(async s => resolution = await s.ResolveForEmployeeAsync(ApprovalApplicationType.Leave, _employeeId, default));

        resolution!.Winner.Should().BeNull();
    }

    [Fact]
    public void SamePriorityOverlaps_AreReported_OnlyBetweenActiveWorkflowsThatCanCollide()
    {
        var a = new ApprovalWorkflow { Id = Guid.NewGuid(), Name = "A", IsActive = true, Priority = 10, Scopes = [new() { Dimension = ApprovalScopeDimension.Branch, TargetId = BranchA }] };
        var hr = new ApprovalWorkflow { Id = Guid.NewGuid(), Name = "HR", IsActive = true, Priority = 10, Scopes = [new() { Dimension = ApprovalScopeDimension.Department, TargetId = Guid.NewGuid() }] };
        var b = new ApprovalWorkflow { Id = Guid.NewGuid(), Name = "B", IsActive = true, Priority = 10, Scopes = [new() { Dimension = ApprovalScopeDimension.Branch, TargetId = BranchB }] };
        var other = new ApprovalWorkflow { Id = Guid.NewGuid(), Name = "Other priority", IsActive = true, Priority = 20 };

        var overlaps = ApprovalWorkflowService.FindSamePriorityOverlaps([a, hr, b, other]);

        overlaps[a.Id].Should().BeEquivalentTo(["HR"]);
        overlaps[b.Id].Should().BeEquivalentTo(["HR"]);
        overlaps[hr.Id].Should().BeEquivalentTo(["A", "B"]);
        overlaps[other.Id].Should().BeEmpty();
    }
}
