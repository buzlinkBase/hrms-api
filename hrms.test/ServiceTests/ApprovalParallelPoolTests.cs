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
/// Parallel approver pools: a step can need approval from several different groups at once (say one
/// from HR and one from Finance). The step's own approver is the first pool and each extra group
/// another; the step clears only when every pool has its quorum, in any order.
/// </summary>
public class ApprovalParallelPoolTests : IDisposable
{
    // ---- pure pool rules ----

    private static ApprovalAction Approval(Guid? group, Guid actor, Guid? onBehalfOf = null) => new()
    {
        Id = Guid.NewGuid(), StepNumber = 1, CycleNumber = 1, Action = ApprovalActionType.Approved,
        GroupId = group, ActorEmployeeId = actor, OnBehalfOfEmployeeId = onBehalfOf,
    };

    private static ApprovalWorkflowStep TwoGroupStep(out Guid g1, out Guid g2)
    {
        g1 = Guid.NewGuid(); g2 = Guid.NewGuid();
        return new ApprovalWorkflowStep
        {
            StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = Guid.NewGuid(), MinApprovals = 1, NamedApprovers = [],
            Groups =
            [
                new ApprovalWorkflowStepGroup { Id = g2, GroupNumber = 2, ApproverType = ApproverType.Person, ApproverEmployeeId = Guid.NewGuid(), MinApprovals = 1 },
                new ApprovalWorkflowStepGroup { Id = g1, GroupNumber = 1, ApproverType = ApproverType.Department, ApproverDepartmentId = Guid.NewGuid(), MinApprovals = 2 },
            ],
        };
    }

    [Fact]
    public void Pools_AreTheStepsOwnApproverFirst_ThenTheGroupsInOrder_WithTheirOwnQuorums()
    {
        var step = TwoGroupStep(out var g1, out var g2);

        var pools = ApprovalStepPools.Of(step);

        pools.Select(p => p.GroupId).Should().Equal(null, g1, g2);
        pools.Select(p => p.MinApprovals).Should().Equal(1, 2, 1);
        pools[1].Effective.ApproverType.Should().Be(ApproverType.Department);
        pools[1].Effective.StepNumber.Should().Be(1);
        ApprovalStepPools.IsParallel(step).Should().BeTrue();
        ApprovalStepPools.IsParallel(new ApprovalWorkflowStep { NamedApprovers = [] }).Should().BeFalse();
    }

    [Fact]
    public void AnApproval_CountsForItsOwnPoolOnly()
    {
        var step = TwoGroupStep(out var g1, out _);
        var pools = ApprovalStepPools.Of(step);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();

        var progress = ApprovalStepPools.Progress(pools, [Approval(null, a), Approval(g1, b)]);

        progress.Select(p => p.Approved).Should().Equal(1, 1, 0);
        progress.Select(p => p.Satisfied).Should().Equal(true, false, false);
        ApprovalStepPools.IsCleared(progress).Should().BeFalse();
    }

    [Fact]
    public void APersonCountsOncePerPool_EvenActingTwiceOrForADelegator()
    {
        var step = TwoGroupStep(out var g1, out _);
        var pools = ApprovalStepPools.Of(step);
        var ana = Guid.NewGuid(); var delegateOfAna = Guid.NewGuid();

        var progress = ApprovalStepPools.Progress(pools, [Approval(g1, ana), Approval(g1, delegateOfAna, onBehalfOf: ana)]);

        progress[1].Approved.Should().Be(1, "the delegate acted for Ana, who had already approved");
    }

    [Fact]
    public void AWholeStepApproval_SatisfiesEveryPool()
    {
        var step = TwoGroupStep(out _, out _);
        var progress = ApprovalStepPools.Progress(ApprovalStepPools.Of(step), [Approval(ApprovalStepPools.WholeStep, Guid.NewGuid())]);

        ApprovalStepPools.IsCleared(progress).Should().BeTrue();
    }

    [Fact]
    public void ADroppedPool_NoLongerCounts_AndEveryRemainingPoolMustStillBeSatisfied()
    {
        var step = TwoGroupStep(out var g1, out var g2);
        var pools = ApprovalStepPools.Of(step);

        var progress = ApprovalStepPools.Progress(pools, [Approval(null, Guid.NewGuid())], new HashSet<Guid> { g1 });

        progress[1].Dropped.Should().BeTrue();
        ApprovalStepPools.IsCleared(progress).Should().BeFalse("the third pool still needs its approval");
        ApprovalStepPools.IsCleared(ApprovalStepPools.Progress(
            pools, [Approval(null, Guid.NewGuid()), Approval(g2, Guid.NewGuid())], new HashSet<Guid> { g1 })).Should().BeTrue();
    }

    // ---- engine on SQLite ----

    private readonly SqliteHrmsContext _db = new();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _deptId = Guid.NewGuid();
    private int _bio;
    private Employee _ana = null!, _ben = null!, _hana = null!, _cara = null!, _dina = null!, _eli = null!, _admin = null!;

    public ApprovalParallelPoolTests()
    {
        _ana = Make("Ana"); _ben = Make("Ben"); _hana = Make("Hana"); _admin = Make("Admin");
        _cara = Make("Cara", dept: true); _dina = Make("Dina", dept: true); _eli = Make("Eli");
        using var seed = _db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = _groupId, Name = "Main" });
        seed.Departments.Add(new Department { Id = _deptId, Code = "FIN", Name = "Finance" });
        seed.SaveChanges();
        seed.Employees.AddRange(_ana, _ben, _hana, _cara, _dina, _eli, _admin);
        seed.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private Employee Make(string first, bool dept = false) => new()
    {
        Id = Guid.NewGuid(), PayrollGroupId = _groupId, JobLevel = "", FirstName = first, LastName = "X", BioId = ++_bio,
        EmployeeNo = $"E{_bio:000}", Email = $"{first.ToLower()}@acme.com", UserId = Guid.NewGuid(), DepartmentId = dept ? _deptId : null,
    };

    // Step 1: Ben (own pool) + Hana (group 1) must both approve.
    private async Task<Guid> SeedAsync(params ApprovalWorkflowStepGroup[] groups)
    {
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = "Flow", IsActive = true,
            Steps =
            [
                new ApprovalWorkflowStep
                {
                    Id = Guid.NewGuid(), StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = _ben.Id, MinApprovals = 1,
                    Groups = groups.ToList(),
                },
            ],
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

    private ApprovalWorkflowStepGroup PersonGroup(int number, Employee who) => new()
    {
        Id = Guid.NewGuid(), GroupNumber = number, ApproverType = ApproverType.Person, ApproverEmployeeId = who.Id, MinApprovals = 1,
    };

    private (ApprovalEngineService Engine, IPublishEndpoint Publisher, UnitOfWorkService Uow, HrmsContext Context) NewEngine()
    {
        var context = _db.NewContext();
        var uow = new UnitOfWorkService(context);
        var publisher = Substitute.For<IPublishEndpoint>();
        return (new ApprovalEngineService(uow, publisher), publisher, uow, context);
    }

    private async Task<ApprovalActionResult> ActAsync(Guid applicationId, Employee who, bool admin = false,
        ApprovalActionType action = ApprovalActionType.Approved)
    {
        var (engine, _, uow, context) = NewEngine();
        await using var _ = context;
        var result = await engine.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, _ana.Id, who.Id, admin, action, null, default);
        await uow.CommitChangesAsync("", default);
        return result;
    }

    private async Task<bool> EligibleAsync(Guid applicationId, Employee who)
    {
        var (engine, _, _, context) = NewEngine();
        await using var _ = context;
        return await engine.IsCallerEligibleAsync(ApprovalApplicationType.Leave, applicationId, who.Id);
    }

    [Fact]
    public async Task EveryPoolMustApprove_InAnyOrder()
    {
        var applicationId = await SeedAsync(PersonGroup(1, _hana));

        (await ActAsync(applicationId, _hana)).InstanceStatus.Should().Be(ApprovalInstanceStatus.InProgress);
        (await ActAsync(applicationId, _ben)).InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);

        var second = await SeedAsync(PersonGroup(1, _hana));
        (await ActAsync(second, _ben)).InstanceStatus.Should().Be(ApprovalInstanceStatus.InProgress);
        (await ActAsync(second, _hana)).InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);

        await using var read = _db.NewContext();
        var groupIds = await read.ApprovalActions.AsNoTracking().Where(a => a.Action == ApprovalActionType.Approved).Select(a => a.GroupId).ToListAsync();
        groupIds.Count(g => g == null).Should().Be(2, "Ben approved for the step's own pool both times");
        groupIds.Count(g => g != null).Should().Be(2);
    }

    [Fact]
    public async Task APoolWithAQuorum_NeedsThatManyDistinctApprovers()
    {
        var applicationId = await SeedAsync(new ApprovalWorkflowStepGroup
        {
            Id = Guid.NewGuid(), GroupNumber = 1, ApproverType = ApproverType.Department, ApproverDepartmentId = _deptId, MinApprovals = 2,
        });
        await ActAsync(applicationId, _ben);

        (await ActAsync(applicationId, _cara)).InstanceStatus.Should().Be(ApprovalInstanceStatus.InProgress, "Finance needs two");
        (await ActAsync(applicationId, _dina)).InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task APersonInTwoPools_FillsOnlyOne_AndCannotApproveTwice()
    {
        // Ben is the step's own approver AND named again in group 1.
        var applicationId = await SeedAsync(PersonGroup(1, _ben));

        (await ActAsync(applicationId, _ben)).InstanceStatus.Should().Be(ApprovalInstanceStatus.InProgress);
        (await ActAsync(applicationId, _ben)).InstanceStatus.Should().Be(ApprovalInstanceStatus.InProgress, "a second click is ignored");

        await using var read = _db.NewContext();
        (await read.ApprovalActions.CountAsync(a => a.Action == ApprovalActionType.Approved)).Should().Be(1);
    }

    [Fact]
    public async Task APoolMemberWhosePoolIsAlreadyFull_IsNoLongerNeeded()
    {
        var applicationId = await SeedAsync(PersonGroup(1, _hana));
        await ActAsync(applicationId, _ben);

        (await EligibleAsync(applicationId, _hana)).Should().BeTrue();
        (await EligibleAsync(applicationId, _eli)).Should().BeFalse();
        (await EligibleAsync(applicationId, _ben)).Should().BeFalse("Ben's pool is satisfied");
    }

    [Fact]
    public async Task ADeclineByAnyPool_DeclinesTheRequest()
    {
        var applicationId = await SeedAsync(PersonGroup(1, _hana));

        (await ActAsync(applicationId, _hana, action: ApprovalActionType.Declined)).InstanceStatus.Should().Be(ApprovalInstanceStatus.Declined);
    }

    [Fact]
    public async Task AnAdminOverride_DecidesForTheWholeStep()
    {
        var applicationId = await SeedAsync(PersonGroup(1, _hana));

        (await ActAsync(applicationId, _admin, admin: true)).InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);

        await using var read = _db.NewContext();
        (await read.ApprovalActions.AsNoTracking().SingleAsync()).GroupId.Should().Be(ApprovalStepPools.WholeStep);
    }

    [Fact]
    public async Task AReassignedApprover_DecidesForTheWholeStep_AndNobodyElseCanAct()
    {
        var applicationId = await SeedAsync(PersonGroup(1, _hana));
        await using (var edit = _db.NewContext())
            await edit.ApprovalInstances.ExecuteUpdateAsync(s => s.SetProperty(i => i.ReassignedApproverEmployeeId, _eli.Id));

        (await EligibleAsync(applicationId, _ben)).Should().BeFalse();
        (await EligibleAsync(applicationId, _eli)).Should().BeTrue();
        (await ActAsync(applicationId, _eli)).InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task AnEscalatedPerson_DecidesForTheWholeStep_WhileThePoolsStillCan()
    {
        var applicationId = await SeedAsync(PersonGroup(1, _hana));
        await using (var edit = _db.NewContext())
            await edit.ApprovalInstances.ExecuteUpdateAsync(s => s.SetProperty(i => i.EscalatedToEmployeeId, _eli.Id));

        (await EligibleAsync(applicationId, _ben)).Should().BeTrue();
        (await ActAsync(applicationId, _eli)).InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task APoolSetToSkip_ThatFindsNobody_IsDropped_SoTheOthersAloneClearTheStep()
    {
        var applicationId = await SeedAsync(new ApprovalWorkflowStepGroup
        {
            Id = Guid.NewGuid(), GroupNumber = 1, ApproverType = ApproverType.ApplicantBranchHead, MinApprovals = 1,
            OnUnresolved = UnresolvedApproverPolicy.Skip,
        });

        // Ana's branch has no head, so that pool is dropped and Ben alone clears the step.
        (await ActAsync(applicationId, _ben)).InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task EveryPoolsPeople_AreToldWhenTheStepOpens()
    {
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = "Flow", IsActive = true,
            Steps =
            [
                new ApprovalWorkflowStep
                {
                    Id = Guid.NewGuid(), StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = _ben.Id, MinApprovals = 1,
                    Groups = [PersonGroup(1, _hana), PersonGroup(2, _ben)],
                },
            ],
        };
        await using (var seed = _db.NewContext())
        {
            seed.ApprovalWorkflows.Add(workflow);
            await seed.SaveChangesAsync();
        }

        var (engine, publisher, _, context) = NewEngine();
        await using (context)
            await engine.StartAsync(ApprovalApplicationType.Leave, Guid.NewGuid(), _ana.Id, default);

        await publisher.Received(1).Publish(Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "ben@acme.com"), Arg.Any<CancellationToken>());
        await publisher.Received(1).Publish(Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "hana@acme.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PoolProgress_ShowsWhoIsStillNeeded()
    {
        var applicationId = await SeedAsync(PersonGroup(1, _hana));
        await ActAsync(applicationId, _ben);

        var (engine, _, _, context) = NewEngine();
        await using (context)
        {
            var instance = (await engine.GetInstanceReadOnlyAsync(ApprovalApplicationType.Leave, applicationId))!;
            var progress = (await engine.CurrentPoolProgressAsync(instance, default))!;
            progress.Select(p => (p.Approved, p.Required, p.Satisfied)).Should().Equal((1, 1, true), (0, 1, false));
        }
    }

    [Fact]
    public async Task AnOrdinaryStep_HasNoPoolProgress_AndBehavesAsBefore()
    {
        var applicationId = await SeedAsync();

        var (engine, _, _, context) = NewEngine();
        await using (context)
        {
            var instance = (await engine.GetInstanceReadOnlyAsync(ApprovalApplicationType.Leave, applicationId))!;
            (await engine.CurrentPoolProgressAsync(instance, default)).Should().BeNull();
        }
        (await ActAsync(applicationId, _ben)).InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    // ---- saving a workflow with groups ----

    private async Task SaveAsync(params ApprovalWorkflowStepGroup[] groups)
    {
        await using var context = _db.NewContext();
        await new ApprovalWorkflowService(new UnitOfWorkService(context)).CreateWorkflowAsync(new ApprovalWorkflow
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = "F", IsActive = false,
            Steps = [new ApprovalWorkflowStep { Id = Guid.NewGuid(), StepNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = _ben.Id, Groups = groups.ToList() }],
        }, default);
    }

    [Fact]
    public async Task Saving_PersistsTheGroupsInOrder()
    {
        await SaveAsync(PersonGroup(1, _hana), new ApprovalWorkflowStepGroup
        {
            Id = Guid.NewGuid(), GroupNumber = 2, ApproverType = ApproverType.Department, ApproverDepartmentId = _deptId, MinApprovals = 2,
        });

        await using var read = _db.NewContext();
        var groups = await read.ApprovalWorkflowStepGroups.AsNoTracking().OrderBy(g => g.GroupNumber).ToListAsync();
        groups.Select(g => (g.GroupNumber, g.ApproverType, g.MinApprovals)).Should().Equal(
            (1, ApproverType.Person, 1), (2, ApproverType.Department, 2));
    }

    [Fact]
    public async Task Saving_RejectsABadGroup()
    {
        await FluentActions.Awaiting(() => SaveAsync(new ApprovalWorkflowStepGroup { GroupNumber = 1, ApproverType = ApproverType.Person, MinApprovals = 1 }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*group 1*approver must be selected*");
        await FluentActions.Awaiting(() => SaveAsync(new ApprovalWorkflowStepGroup { GroupNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = _hana.Id, MinApprovals = 0 }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*at least 1 approval*");
        await FluentActions.Awaiting(() => SaveAsync(new ApprovalWorkflowStepGroup
            { GroupNumber = 1, ApproverType = ApproverType.Person, ApproverEmployeeId = _hana.Id, MinApprovals = 1, OnUnresolved = UnresolvedApproverPolicy.Skip }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*per applicant*");
        await FluentActions.Awaiting(() => SaveAsync(new ApprovalWorkflowStepGroup
            { GroupNumber = 1, ApproverType = ApproverType.ApplicantBranchHead, MinApprovals = 1, OnUnresolved = UnresolvedApproverPolicy.FallbackPerson }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*person to use*");
        await FluentActions.Awaiting(() => SaveAsync(Enumerable.Range(1, 6).Select(PersonGroupNumber).ToArray()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*at most 5*");
    }

    private ApprovalWorkflowStepGroup PersonGroupNumber(int n) => PersonGroup(n, _hana);

    [Fact]
    public async Task Saving_AGroupMayAskToSkipEvenOnTheFirstStep()
    {
        await SaveAsync(new ApprovalWorkflowStepGroup
        {
            Id = Guid.NewGuid(), GroupNumber = 1, ApproverType = ApproverType.ApplicantAreaHead, MinApprovals = 1, OnUnresolved = UnresolvedApproverPolicy.Skip,
        });

        await using var read = _db.NewContext();
        (await read.ApprovalWorkflowStepGroups.CountAsync()).Should().Be(1);
    }
}
