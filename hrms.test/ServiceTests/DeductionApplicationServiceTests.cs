using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using MapsterMapper;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// DeductionApplicationService.WithdrawAsync — the self-service cancel path, which stays on the
/// standard GetOneAsync + ModifyAsync pattern and so is fully testable with the same IRepository
/// mocking used everywhere else in this project.
///
/// ApproveAsync/DeclineAsync now route through ApprovalEngineService (the configurable
/// multi-level approval engine), which reads/writes via raw Context.ApprovalInstances/
/// Context.Employees/etc. (real EF DbContext access, not IRepository) — the same class of
/// Context-based limitation already flagged below for DeductionAplDtlService.LoadAsync, so they
/// aren't unit-tested here either. See ApprovalEligibilityTests for the engine's own decision
/// logic (eligibility/quorum), which IS fully unit-tested since it takes plain in-memory objects
/// with no DB access at all.
///
/// DeductionAplDtlService.LoadAsync — the actual payroll gate that reads ApprovalStatus off
/// DeductionApplication — is NOT unit-tested here: it joins against raw Context.DeductionApplications/
/// Context.Deductions/Context.DeductionTypes (real EF DbContext access, not IRepository), and
/// HrmsContext requires a tenant/soft-delete global query filter (UseTenantAndDateFilter) that
/// would need its own EF-InMemory + ITenantProvider test harness to seed correctly — the same
/// class of limitation already flagged for ChangeRestDayService's Context-based methods. This
/// was called out to the user rather than silently skipped.
/// </summary>
public class DeductionApplicationServiceTests
{
    private static Employee BuildEmployee(Guid id) => new()
    {
        Id = id,
        UserId = Guid.NewGuid(),
        EmployeeNo = "EMP-001",
        Skills = [],
        Educations = [],
        Dependents = [],
        EmployeeRecords = [],
        Employments = [],
        Assets = [],
        RestDays = [],
    };

    private static Deduction BuildDeduction(Guid id) => new()
    {
        Id = id,
        Code = "COLOAN",
        Name = "Company Loan",
    };

    private static DeductionApplication BuildApplication(Guid id, Guid employeeId, Guid deductionId, ApprovalStatus status) => new()
    {
        Id = id,
        EmployeeId = employeeId,
        DeductionId = deductionId,
        StartDate = new DateOnly(2026, 1, 1),
        EndDate = new DateOnly(2026, 12, 31),
        Terms = 0,
        Breakdown = [],
        ApprovalStatus = status,
    };

    private static DeductionApplicationService BuildService(DeductionApplication application, Employee employee, Deduction deduction)
    {
        var repo = Substitute.For<IRepository>();
        repo.FindOneAsync<DeductionApplication>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Guid>() == application.Id ? application : null);
        repo.FindAll<Employee>().Returns(_ => new List<Employee> { employee }.BuildMockDbSet());
        repo.FindOneAsync<Deduction>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Guid>() == deduction.Id ? deduction : null);

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);
        uow.CommitChangesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        TypeAdapterConfig.GlobalSettings.Scan(typeof(MappingProfile).Assembly);

        var employeeService = new EmployeeService(
            uow,
            Substitute.For<IMapper>(),
            TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow),
            new PayrollGroupService(uow),
            new BranchService(uow),
            new CostCenterService(uow),
            new PositionService(uow),
            new SectionService(uow));
        var deductionService = new DeductionService(uow);
        var approvalEngine = new ApprovalEngineService(uow);

        return new DeductionApplicationService(uow, deductionService, employeeService, Substitute.For<IMapper>(), approvalEngine);
    }

    [Fact]
    public async Task WithdrawAsync_SetsStatusToWithdrawn()
    {
        var employeeId = Guid.NewGuid();
        var deductionId = Guid.NewGuid();
        var application = BuildApplication(Guid.NewGuid(), employeeId, deductionId, ApprovalStatus.ForApproval);
        var service = BuildService(application, BuildEmployee(employeeId), BuildDeduction(deductionId));

        await service.WithdrawAsync(application.Id, employeeId, CancellationToken.None);

        application.ApprovalStatus.Should().Be(ApprovalStatus.Withdrawn);
    }

    [Fact]
    public async Task WithdrawAsync_RejectsWhenNotOwnedByCaller()
    {
        var employeeId = Guid.NewGuid();
        var deductionId = Guid.NewGuid();
        var application = BuildApplication(Guid.NewGuid(), employeeId, deductionId, ApprovalStatus.ForApproval);
        var service = BuildService(application, BuildEmployee(employeeId), BuildDeduction(deductionId));

        var act = () => service.WithdrawAsync(application.Id, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        application.ApprovalStatus.Should().Be(ApprovalStatus.ForApproval);
    }

    [Fact]
    public async Task WithdrawAsync_RejectsWhenNotPending()
    {
        var employeeId = Guid.NewGuid();
        var deductionId = Guid.NewGuid();
        var application = BuildApplication(Guid.NewGuid(), employeeId, deductionId, ApprovalStatus.Approved);
        var service = BuildService(application, BuildEmployee(employeeId), BuildDeduction(deductionId));

        var act = () => service.WithdrawAsync(application.Id, employeeId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        application.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
    }
}
