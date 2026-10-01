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
}
