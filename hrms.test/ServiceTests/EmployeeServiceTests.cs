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

    // Production outage: EmployeeFullModel.RestDays used to be the RestDay *entity*, whose
    // virtual Employee navigation let the JSON serializer lazy-load Employee -> Manager ->
    // DirectReports -> ... on every GET me/employee -- a RAM spike and a 60s+ hang that left My
    // Profile blank. RestDays must come back as the DTO, carrying only Id/DayName.
    [Fact]
    public async Task GetFullByUserOrEmailAsync_ProjectsRestDaysToDto_WithoutTheEmployeeGraph()
    {
        var userId = Guid.NewGuid();
        var employee = BuildEmployee(userId: userId);
        var restDay = new RestDay { Id = Guid.NewGuid(), DayName = DayName.Sunday, EmployeeId = employee.Id, Employee = employee };
        employee.RestDays = [restDay];
        var service = BuildService(SeedRepo(employee));

        var result = await service.GetFullByUserOrEmailAsync(userId, null, CancellationToken.None);

        var projected = result!.RestDays.Should().ContainSingle().Subject;
        projected.Should().BeOfType<RestDayModel>();
        projected.Id.Should().Be(restDay.Id);
        projected.DayName.Should().Be(DayName.Sunday);

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(result.RestDays);
        json.Should().NotContain("Employee", "the rest-day payload must not drag the employee graph along");
    }

    // Guard against any EF entity creeping back into the portal/201 payload -- one entity-typed
    // member (with lazy-loading proxies on) is all it takes to serialize half the database.
    [Fact]
    public void EmployeeFullModel_ExposesNoEntityTypes()
    {
        static Type Unwrap(Type t) =>
            t.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(t) && t != typeof(string)
                ? t.GetGenericArguments()[0]
                : Nullable.GetUnderlyingType(t) ?? t;

        var entityNamespace = typeof(Employee).Namespace!.Split('.').Take(3).Aggregate((a, b) => $"{a}.{b}"); // "Hrms.Domain.Entities"
        var offenders = typeof(EmployeeFullModel).GetProperties()
            .Where(p => Unwrap(p.PropertyType).Namespace?.StartsWith(entityNamespace) == true)
            .Select(p => $"{p.Name}: {p.PropertyType.Name}")
            .ToList();

        offenders.Should().BeEmpty();
    }

    /// <summary>
    /// EmployeeService.CreateValidatorAsync's Email-uniqueness check -- ResolveEmployeeIdAsync
    /// (and the approval engine's own approver-resolution, same pattern) matches an Employee by
    /// Email, so two employees sharing one email would make that resolution ambiguous. Exercised
    /// through the public AddAsync entry point (CreateValidatorAsync itself is protected), which
    /// runs this check before the PayrollGroup requirement further down the same method -- so a
    /// rejection here never even reaches that later check, and passing this check but failing the
    /// next one is how "the email check let it through" is proven without needing to stub a full
    /// PayrollGroup.
    /// </summary>
    [Fact]
    public async Task AddAsync_DuplicateEmail_ThrowsValidationException()
    {
        var existing = BuildEmployee(email: "dup@company.com");
        var service = BuildService(SeedRepo(existing));
        var incoming = BuildEmployee(email: "dup@company.com", employeeNo: "EMP-002");

        var act = () => service.AddAsync(incoming, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Email is already used*");
    }

    [Fact]
    public async Task AddAsync_DuplicateEmail_IsCaseInsensitive()
    {
        var existing = BuildEmployee(email: "Dup@Company.com");
        var service = BuildService(SeedRepo(existing));
        var incoming = BuildEmployee(email: "dup@company.com", employeeNo: "EMP-002");

        var act = () => service.AddAsync(incoming, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Email is already used*");
    }

    [Fact]
    public async Task AddAsync_UniqueEmail_PassesEmailCheck()
    {
        var existing = BuildEmployee(email: "someone@company.com");
        var service = BuildService(SeedRepo(existing));
        var incoming = BuildEmployee(email: "different@company.com", employeeNo: "EMP-002");

        var act = () => service.AddAsync(incoming, CancellationToken.None);

        // No PayrollGroup seeded, so the next validator step still rejects -- but with the
        // PayrollGroup message, not the email one, proving the email check itself passed.
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Payroll group is required");
    }

    [Fact]
    public async Task AddAsync_MultipleEmployeesWithNoEmail_DoNotCollide()
    {
        var existing = BuildEmployee(email: null);
        var service = BuildService(SeedRepo(existing));
        var incoming = BuildEmployee(email: null, employeeNo: "EMP-002");

        var act = () => service.AddAsync(incoming, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Payroll group is required");
    }
}
