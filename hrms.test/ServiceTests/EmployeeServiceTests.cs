using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using MapsterMapper;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// EmployeeService.GetFullByUserOrEmailAsync / ResolveEmployeeIdAsync — resolve the
/// self-service portal caller's own Employee record from the JWT's UserId (sub claim,
/// primary — backfilled by UserOnboardedWorker) or Email (fallback, for employees whose
/// UserId link hasn't been made yet). No production code exercises this predicate before
/// MeController, so these tests cover the resolution priority directly against the service.
/// </summary>
public class EmployeeServiceTests
{
    private static Employee BuildEmployee(Guid? userId = null, string? email = null, string employeeNo = "EMP-001") => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Email = email,
        EmployeeNo = employeeNo,
        Skills = [],
        Educations = [],
        Dependents = [],
        EmployeeRecords = [],
        Employments = [],
        Assets = [],
        RestDays = [],
    };

    private static EmployeeService BuildService(IRepository repo)
    {
        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        TypeAdapterConfig.GlobalSettings.Scan(typeof(MappingProfile).Assembly);

        return new EmployeeService(
            uow,
            Substitute.For<IMapper>(),
            TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow),
            new PayrollGroupService(uow),
            new BranchService(uow),
            new CostCenterService(uow),
            new PositionService(uow),
            new SectionService(uow));
    }

    private static IRepository SeedRepo(params Employee[] employees)
    {
        var repo = Substitute.For<IRepository>();
        // BuildMockDbSet() itself uses NSubstitute internally — evaluating it eagerly as a
        // plain argument to Returns() clobbers the pending "last call" NSubstitute is tracking
        // for FindAll<Employee>(), so it must be deferred inside the Returns(callInfo => ...)
        // lambda instead (same pattern as LeaveWorkerTestBase.SeedFind).
        repo.FindAll<Employee>().Returns(_ => employees.ToList().BuildMockDbSet());
        return repo;
    }

    [Fact]
    public async Task ResolveEmployeeIdAsync_MatchesByUserId_WhenSet()
    {
        var userId = Guid.NewGuid();
        var employee = BuildEmployee(userId: userId, email: "someone@else.com");
        var otherEmployee = BuildEmployee(email: "caller@company.com");
        var service = BuildService(SeedRepo(employee, otherEmployee));

        var resolved = await service.ResolveEmployeeIdAsync(userId, "caller@company.com", CancellationToken.None);

        resolved.Should().Be(employee.Id);
    }

    [Fact]
    public async Task ResolveEmployeeIdAsync_FallsBackToEmail_WhenUserIdUnset()
    {
        var employee = BuildEmployee(userId: null, email: "caller@company.com");
        var service = BuildService(SeedRepo(employee));

        var resolved = await service.ResolveEmployeeIdAsync(Guid.NewGuid(), "caller@company.com", CancellationToken.None);

        resolved.Should().Be(employee.Id);
    }

    [Fact]
    public async Task ResolveEmployeeIdAsync_ReturnsNull_WhenNeitherMatches()
    {
        var employee = BuildEmployee(userId: Guid.NewGuid(), email: "someone@else.com");
        var service = BuildService(SeedRepo(employee));

        var resolved = await service.ResolveEmployeeIdAsync(Guid.NewGuid(), "caller@company.com", CancellationToken.None);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task GetFullByUserOrEmailAsync_ReturnsProjectedEmployee_WhenMatched()
    {
        var userId = Guid.NewGuid();
        var employee = BuildEmployee(userId: userId, employeeNo: "EMP-042");
        var service = BuildService(SeedRepo(employee));

        var result = await service.GetFullByUserOrEmailAsync(userId, null, CancellationToken.None);

        result.Should().NotBeNull();
        result!.EmployeeNo.Should().Be("EMP-042");
    }
}
