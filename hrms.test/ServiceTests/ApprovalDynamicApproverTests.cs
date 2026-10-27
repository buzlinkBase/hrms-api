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
/// Dynamic approver steps: the manager N levels up and the head of the applicant's department,
/// section, branch, site or client. Resolved per applicant; when nobody is found the step blocks,
/// skips (never the first step) or hands over to a fallback person.
/// </summary>
public class ApprovalDynamicApproverTests : IDisposable
{
    // ---- pure resolver over a fake org ----

    private sealed class FakeOrg : IApproverOrgLookup
    {
        public Dictionary<Guid, Employee> Employees { get; } = [];
        public Dictionary<Guid, DepartmentNode> Departments { get; } = [];
        public Dictionary<Guid, Guid?> Sections { get; } = [];
        public Dictionary<Guid, Guid?> Branches { get; } = [];
        public Dictionary<Guid, Guid?> Areas { get; } = [];
        public Dictionary<Guid, Guid?> Clients { get; } = [];

        public Task<Employee?> GetEmployeeAsync(Guid id, CancellationToken token) => Task.FromResult(Employees.GetValueOrDefault(id));
        public Task<DepartmentNode?> GetDepartmentAsync(Guid id, CancellationToken token) => Task.FromResult(Departments.GetValueOrDefault(id));
        public Task<Guid?> GetSectionHeadAsync(Guid id, CancellationToken token) => Task.FromResult(Sections.GetValueOrDefault(id));
        public Task<Guid?> GetBranchHeadAsync(Guid id, CancellationToken token) => Task.FromResult(Branches.GetValueOrDefault(id));
        public Task<Guid?> GetAreaHeadAsync(Guid id, CancellationToken token) => Task.FromResult(Areas.GetValueOrDefault(id));
        public Task<Guid?> GetClientHeadAsync(Guid id, CancellationToken token) => Task.FromResult(Clients.GetValueOrDefault(id));
    }

    private static Employee Emp(Guid? manager = null, Guid? dept = null, Guid? section = null, Guid? branch = null, Guid? area = null, Guid? client = null) => new()
    {
        Id = Guid.NewGuid(), ManagerId = manager, DepartmentId = dept, SectionId = section, BranchId = branch, AreaId = area, ClientId = client,
        PayrollGroupId = Guid.NewGuid(), JobLevel = "", FirstName = "T", LastName = "E",
    };

    private static ApprovalWorkflowStep Step(ApproverType type, int? level = null,
        UnresolvedApproverPolicy policy = UnresolvedApproverPolicy.Block, Guid? fallback = null) => new()
    {
        StepNumber = 2, ApproverType = type, ApproverLevel = level, OnUnresolved = policy, FallbackEmployeeId = fallback, NamedApprovers = [],
    };

    private static async Task<ResolvedApprovers> Resolve(FakeOrg org, ApprovalWorkflowStep step, Employee applicant) =>
        await new ApprovalApproverResolver(org).ResolveAsync(step, applicant, default);

    [Fact]
    public async Task ManagerLevel_WalksTheReportingLine()
    {
        var org = new FakeOrg();
        var top = Emp(); var mid = Emp(manager: top.Id); var boss = Emp(manager: mid.Id); var staff = Emp(manager: boss.Id);
        foreach (var e in new[] { top, mid, boss, staff }) org.Employees[e.Id] = e;

        (await Resolve(org, Step(ApproverType.ApplicantManagerLevel, 1), staff)).EmployeeIds.Should().Equal(boss.Id);
        (await Resolve(org, Step(ApproverType.ApplicantManagerLevel, 2), staff)).EmployeeIds.Should().Equal(mid.Id);
        (await Resolve(org, Step(ApproverType.ApplicantManagerLevel, 3), staff)).EmployeeIds.Should().Equal(top.Id);
        (await Resolve(org, Step(ApproverType.ApplicantManagerLevel, 4), staff)).Unresolved.Should().BeTrue("the line ends at the top");
        (await Resolve(org, Step(ApproverType.ApplicantManagerLevel), staff)).EmployeeIds.Should().Equal(boss.Id);
    }

    [Fact]
    public async Task ManagerLevel_StopsOnALoop()
    {
        var org = new FakeOrg();
        var a = Emp(); var b = Emp(manager: a.Id); a.ManagerId = b.Id;
        org.Employees[a.Id] = a; org.Employees[b.Id] = b;

        (await Resolve(org, Step(ApproverType.ApplicantManagerLevel, 2), a)).Unresolved.Should().BeTrue("A -> B -> A would be the applicant themself");
        (await Resolve(org, Step(ApproverType.ApplicantManagerLevel, 5), b)).Unresolved.Should().BeTrue();
    }

    [Fact]
    public async Task DepartmentHead_ClimbsToTheParentDepartment()
    {
        var org = new FakeOrg();
        var division = Guid.NewGuid(); var dept = Guid.NewGuid();
        var divisionHead = Guid.NewGuid(); var deptHead = Guid.NewGuid();
        org.Departments[division] = new DepartmentNode(divisionHead, null);
        org.Departments[dept] = new DepartmentNode(deptHead, division);
        var staff = Emp(dept: dept);

        (await Resolve(org, Step(ApproverType.ApplicantDepartmentHead, 1), staff)).EmployeeIds.Should().Equal(deptHead);
        (await Resolve(org, Step(ApproverType.ApplicantDepartmentHead, 2), staff)).EmployeeIds.Should().Equal(divisionHead);
        (await Resolve(org, Step(ApproverType.ApplicantDepartmentHead, 3), staff)).Unresolved.Should().BeTrue("no department above the division");
        (await Resolve(org, Step(ApproverType.ApplicantDepartmentHead, 1), Emp())).Unresolved.Should().BeTrue("no department");
    }

    [Fact]
    public async Task SectionBranchAreaAndClientHeads_ResolveFromTheApplicantsOwnUnit()
    {
        var org = new FakeOrg();
        Guid section = Guid.NewGuid(), branch = Guid.NewGuid(), area = Guid.NewGuid(), client = Guid.NewGuid();
        Guid sectionHead = Guid.NewGuid(), branchHead = Guid.NewGuid(), areaHead = Guid.NewGuid(), clientHead = Guid.NewGuid();
        org.Sections[section] = sectionHead; org.Branches[branch] = branchHead; org.Areas[area] = areaHead; org.Clients[client] = clientHead;
        var staff = Emp(section: section, branch: branch, area: area, client: client);

        (await Resolve(org, Step(ApproverType.ApplicantSectionHead), staff)).EmployeeIds.Should().Equal(sectionHead);
        (await Resolve(org, Step(ApproverType.ApplicantBranchHead), staff)).EmployeeIds.Should().Equal(branchHead);
        (await Resolve(org, Step(ApproverType.ApplicantAreaHead), staff)).EmployeeIds.Should().Equal(areaHead);
        (await Resolve(org, Step(ApproverType.ApplicantClientHead), staff)).EmployeeIds.Should().Equal(clientHead);
    }

    [Fact]
    public async Task AHeadFilingTheirOwnRequest_IsNeverTheirOwnApprover()
    {
        var org = new FakeOrg();
        var branch = Guid.NewGuid();
        var head = Emp(branch: branch);
        org.Branches[branch] = head.Id;

        (await Resolve(org, Step(ApproverType.ApplicantBranchHead), head)).Unresolved.Should().BeTrue();
    }

    [Fact]
    public async Task NoHeadSet_IsUnresolved_UnlessAFallbackPersonIsNamed()
    {
        var org = new FakeOrg();
        var branch = Guid.NewGuid();
        org.Branches[branch] = null;
        var staff = Emp(branch: branch);
        var fallback = Guid.NewGuid();

        (await Resolve(org, Step(ApproverType.ApplicantBranchHead), staff)).Unresolved.Should().BeTrue();
        (await Resolve(org, Step(ApproverType.ApplicantBranchHead, policy: UnresolvedApproverPolicy.Skip), staff)).Unresolved.Should().BeTrue();
        var withFallback = await Resolve(org, Step(ApproverType.ApplicantBranchHead, policy: UnresolvedApproverPolicy.FallbackPerson, fallback: fallback), staff);
        withFallback.Unresolved.Should().BeFalse();
        withFallback.EmployeeIds.Should().Equal(fallback);
    }

    [Fact]
    public async Task ApplicantManager_WithNoManager_UsesTheFallbackToo()
    {
        var fallback = Guid.NewGuid();
        var step = Step(ApproverType.ApplicantManager, policy: UnresolvedApproverPolicy.FallbackPerson, fallback: fallback);

        (await Resolve(new FakeOrg(), step, Emp())).EmployeeIds.Should().Equal(fallback);
    }

    [Fact]
    public void ResolvedStrategy_IsEligible_OnlyForThePeopleTheStepResolvedTo()
    {
        var applicant = Emp(); var head = Emp(); var other = Emp();
        var step = Step(ApproverType.ApplicantBranchHead);
        var resolver = new ApproverEligibilityResolver();

        resolver.IsEligible(new ApprovalEligibilityContext(head, applicant, step, [], null, null, [head.Id])).Should().BeTrue();
        resolver.IsEligible(new ApprovalEligibilityContext(other, applicant, step, [], null, null, [head.Id])).Should().BeFalse();
        resolver.IsEligible(new ApprovalEligibilityContext(head, applicant, step, [], null, null, null)).Should().BeFalse("not resolved yet");
    }

    [Fact]
    public void Labels_NameTheRoleAndThePerson()
    {
        ApprovalStepLabels.Dynamic(Step(ApproverType.ApplicantBranchHead), "Ana Cruz", false).Should().Be("Your Branch Head: Ana Cruz");
        ApprovalStepLabels.Dynamic(Step(ApproverType.ApplicantManagerLevel, 2), null, false).Should().Be("Your Manager 2 Levels Up");
        ApprovalStepLabels.Dynamic(Step(ApproverType.ApplicantAreaHead), null, true).Should().Be("Your Project Site Head (no one assigned)");
        ApprovalStepLabels.Dynamic(Step(ApproverType.ApplicantDepartmentHead, 2), null, false).Should().Be("Department Head 1 Level Up");
        ApprovalStepLabels.Dynamic(Step(ApproverType.Person), null, false).Should().BeNull();
    }

    // ---- engine on SQLite ----

    private readonly SqliteHrmsContext _db = new();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private int _bio;

    public void Dispose() => _db.Dispose();

    private Employee Make(string first, Guid? manager = null, bool branch = false) => new()
    {
        Id = Guid.NewGuid(), PayrollGroupId = _groupId, JobLevel = "", FirstName = first, LastName = "X", BioId = ++_bio,
        EmployeeNo = $"E{_bio:000}", Email = $"{first.ToLower()}@acme.com", UserId = Guid.NewGuid(), ManagerId = manager,
        BranchId = branch ? _branchId : null,
    };

    private async Task SeedAsync(Guid? branchHead, params Employee[] employees)
    {
        await using var seed = _db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = _groupId, Name = "Main" });
        seed.Branches.Add(new Branch { Id = _branchId, Code = "HQ", Name = "HQ", HeadEmployeeId = branchHead });
        await seed.SaveChangesAsync();
        seed.Employees.AddRange(employees);
        await seed.SaveChangesAsync();
    }

    private async Task<(Guid ApplicationId, Guid WorkflowId)> SeedFlowAsync(Employee applicant, params ApprovalWorkflowStep[] steps)
    {
        var workflow = new ApprovalWorkflow
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = "Flow", IsActive = true, Steps = steps.ToList(),
        };
        var applicationId = Guid.NewGuid();
        await using var seed = _db.NewContext();
        seed.ApprovalWorkflows.Add(workflow);
        seed.ApprovalInstances.Add(new ApprovalInstance
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, ApplicationId = applicationId,
            ApplicantEmployeeId = applicant.Id, ApprovalWorkflowId = workflow.Id, CurrentStepNumber = 1,
        });
        await seed.SaveChangesAsync();
        return (applicationId, workflow.Id);
    }

    private static ApprovalWorkflowStep Persisted(int number, ApproverType type, Guid? person = null, int? level = null,
        UnresolvedApproverPolicy policy = UnresolvedApproverPolicy.Block) => new()
    {
        Id = Guid.NewGuid(), StepNumber = number, ApproverType = type, ApproverEmployeeId = person, ApproverLevel = level,
        OnUnresolved = policy, MinApprovals = 1,
    };

    private (ApprovalEngineService Engine, IPublishEndpoint Publisher, UnitOfWorkService Uow, HrmsContext Context) NewEngine()
    {
        var context = _db.NewContext();
        var uow = new UnitOfWorkService(context);
        var publisher = Substitute.For<IPublishEndpoint>();
        return (new ApprovalEngineService(uow, publisher), publisher, uow, context);
    }

    private async Task<ApprovalActionResult> ActAsync(Guid applicationId, Guid applicantId, Guid callerId)
    {
        var (engine, _, uow, context) = NewEngine();
        await using var _ = context;
        var result = await engine.RecordActionAsync(
            ApprovalApplicationType.Leave, applicationId, applicantId, callerId, false, ApprovalActionType.Approved, null, default);
        await uow.CommitChangesAsync(default);
        return result;
    }

    [Fact]
    public async Task ASkippedStep_ThatFindsNobody_IsPassedOver_AndTheRequestCompletes()
    {
        var approver = Make("Ben");
        var staff = Make("Ana", branch: true);
        await SeedAsync(branchHead: null, approver, staff);
        var (applicationId, _) = await SeedFlowAsync(staff,
            Persisted(1, ApproverType.Person, approver.Id),
            Persisted(2, ApproverType.ApplicantBranchHead, policy: UnresolvedApproverPolicy.Skip));

        var result = await ActAsync(applicationId, staff.Id, approver.Id);

        result.InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
        await using var read = _db.NewContext();
        var skipped = await read.ApprovalActions.AsNoTracking().SingleAsync(a => a.Action == ApprovalActionType.Skipped);
        skipped.StepNumber.Should().Be(2);
        skipped.ActorEmployeeId.Should().BeNull();
    }

    [Fact]
    public async Task ASkippedStep_WithAHeadSet_IsNotSkipped_AndTheHeadIsToldAndCanApprove()
    {
        var approver = Make("Ben");
        var head = Make("Hana");
        var staff = Make("Ana", branch: true);
        await SeedAsync(branchHead: head.Id, approver, head, staff);
        var (applicationId, _) = await SeedFlowAsync(staff,
            Persisted(1, ApproverType.Person, approver.Id),
            Persisted(2, ApproverType.ApplicantBranchHead, policy: UnresolvedApproverPolicy.Skip));

        var (engine, publisher, uow, context) = NewEngine();
        await using (context)
        {
            var first = await engine.RecordActionAsync(
                ApprovalApplicationType.Leave, applicationId, staff.Id, approver.Id, false, ApprovalActionType.Approved, null, default);
            await uow.CommitChangesAsync(default);
            first.InstanceStatus.Should().Be(ApprovalInstanceStatus.InProgress);
            first.CurrentStepNumber.Should().Be(2);
            await publisher.Received(1).Publish(
                Arg.Is<ApprovalNotificationRequested>(m => m.RecipientEmail == "hana@acme.com" && m.StatusLabel == ApprovalEngineService.PendingApprovalStatusLabel),
                Arg.Any<CancellationToken>());
        }

        (await ActAsync(applicationId, staff.Id, head.Id)).InstanceStatus.Should().Be(ApprovalInstanceStatus.Approved);
    }

    [Fact]
    public async Task ABlockedStep_WaitsForAnAdmin_AndNoOneButAdminsCanAct()
    {
        var approver = Make("Ben");
        var other = Make("Dan");
        var staff = Make("Ana", branch: true);
        await SeedAsync(branchHead: null, approver, other, staff);
        var (applicationId, _) = await SeedFlowAsync(staff,
            Persisted(1, ApproverType.Person, approver.Id),
            Persisted(2, ApproverType.ApplicantBranchHead));

        (await ActAsync(applicationId, staff.Id, approver.Id)).CurrentStepNumber.Should().Be(2);

        var (engine, _, _, context) = NewEngine();
        await using (context)
        {
            (await engine.IsCallerEligibleAsync(ApprovalApplicationType.Leave, applicationId, other.Id)).Should().BeFalse();
            var instance = (await engine.GetInstanceReadOnlyAsync(ApprovalApplicationType.Leave, applicationId))!;
            var applicant = await context.Employees.AsNoTracking().SingleAsync(e => e.Id == staff.Id);
            (await engine.ResolveCurrentApproversAsync(instance, applicant, default))!.Unresolved.Should().BeTrue();
        }
    }

    [Fact]
    public async Task ASkipLevelManager_IsEligible_AndOthersAreNot()
    {
        var top = Make("Tess");
        var mid = Make("Mia", manager: top.Id);
        var boss = Make("Ben", manager: mid.Id);
        var staff = Make("Ana", manager: boss.Id);
        await SeedAsync(null, top, mid, boss, staff);
        var (applicationId, _) = await SeedFlowAsync(staff, Persisted(1, ApproverType.ApplicantManagerLevel, level: 2));

        var (engine, _, _, context) = NewEngine();
        await using (context)
        {
            (await engine.IsCallerEligibleAsync(ApprovalApplicationType.Leave, applicationId, mid.Id)).Should().BeTrue();
            (await engine.IsCallerEligibleAsync(ApprovalApplicationType.Leave, applicationId, boss.Id)).Should().BeFalse("only two levels up");
            (await engine.IsCallerEligibleAsync(ApprovalApplicationType.Leave, applicationId, top.Id)).Should().BeFalse();
        }
    }

    // ---- saving a workflow ----

    private async Task SaveAsync(ApprovalWorkflow workflow)
    {
        await using var context = _db.NewContext();
        await new ApprovalWorkflowService(new UnitOfWorkService(context)).CreateWorkflowAsync(workflow, default);
    }

    private static ApprovalWorkflow Flow(params ApprovalWorkflowStep[] steps) => new()
    {
        Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = "F", IsActive = false, Steps = steps.ToList(),
    };

    [Fact]
    public async Task Saving_RejectsSkipOnTheFirstStep_AFallbackWithoutAPerson_AndABadLevel()
    {
        await FluentActions.Awaiting(() => SaveAsync(Flow(Persisted(1, ApproverType.ApplicantBranchHead, policy: UnresolvedApproverPolicy.Skip))))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*first step*");
        await FluentActions.Awaiting(() => SaveAsync(Flow(Persisted(1, ApproverType.ApplicantBranchHead, policy: UnresolvedApproverPolicy.FallbackPerson))))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*choose the person*");
        await FluentActions.Awaiting(() => SaveAsync(Flow(Persisted(1, ApproverType.ApplicantManagerLevel, level: 11))))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*level must be*");
        await FluentActions.Awaiting(() => SaveAsync(Flow(Persisted(1, ApproverType.Person, Guid.NewGuid()), Persisted(2, ApproverType.Person, Guid.NewGuid(), policy: UnresolvedApproverPolicy.Skip))))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*look up the approver*");
    }

    [Fact]
    public async Task Saving_AcceptsASkipOnALaterDynamicStep_AndAFallback()
    {
        var approver = Make("Ben");
        await SeedAsync(null, approver);
        await SaveAsync(Flow(
            Persisted(1, ApproverType.Person, approver.Id),
            Persisted(2, ApproverType.ApplicantAreaHead, policy: UnresolvedApproverPolicy.Skip),
            new ApprovalWorkflowStep
            {
                Id = Guid.NewGuid(), StepNumber = 3, ApproverType = ApproverType.ApplicantClientHead,
                OnUnresolved = UnresolvedApproverPolicy.FallbackPerson, FallbackEmployeeId = approver.Id, MinApprovals = 1,
            }));

        await using var read = _db.NewContext();
        (await read.ApprovalWorkflowSteps.CountAsync()).Should().Be(3);
    }
}
