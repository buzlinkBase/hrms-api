using hrms.test.TestSupport;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using MapsterMapper;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// PayrollReportService.GetThirteenthMonthAsync's employeeId filter — the gate that makes
/// MeController.GetMy13thMonth safe to expose to any authenticated employee: without it, this
/// method computes every employee's 13th month figures for the year, so a self-service caller
/// would otherwise need to be trusted not to read someone else's row out of the full result.
/// </summary>
public class PayrollReportServiceTests
{
    private static Employee BuildEmployee(Guid id, string employeeNo) => new()
    {
        Id = id,
        EmployeeNo = employeeNo,
        FirstName = "Test",
        LastName = employeeNo,
        Skills = [],
        Educations = [],
        Dependents = [],
        EmployeeRecords = [],
        Employments = [],
        Assets = [],
        RestDays = [],
    };

    private static Payroll BuildPayroll(Guid employeeId, int year, decimal basicPay) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        PostingPeriod = new DateOnly(year, 6, 15),
        PayPeriodStart = new DateOnly(year, 6, 1),
        PayPeriodEnd = new DateOnly(year, 6, 15),
        IsPosted = true,
        PayrollType = PayrollType.Regular,
        BasicPay = basicPay,
    };

    private static PayrollReportService BuildService(params Payroll[] payrolls) => BuildService(null, payrolls);

    // clients maps an employee to their client; an employee not listed has none.
    private static PayrollReportService BuildService(Dictionary<Guid, Guid>? clients, params Payroll[] payrolls)
    {
        var employees = payrolls.Select(p =>
        {
            var employee = BuildEmployee(p.EmployeeId, p.EmployeeId.ToString()[..8]);
            if (clients != null && clients.TryGetValue(p.EmployeeId, out var clientId)) employee.ClientId = clientId;
            return employee;
        }).ToList();

        var repo = Substitute.For<IRepository>();
        repo.FindAll<Payroll>().Returns(_ => payrolls.ToList().BuildMockDbSet());
        repo.FindAll<PayrollOpeningBalance>().Returns(_ => new List<PayrollOpeningBalance>().BuildMockDbSet());
        repo.FindAll<Employee>().Returns(_ => employees.ToList().BuildMockDbSet());

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        _ = TestMapping.GlobalSettings; // registers MappingProfile once per run (see TestMapping)

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
        var payrollOpeningBalanceService = new PayrollOpeningBalanceService(uow);

        return new PayrollReportService(uow, employeeService, payrollOpeningBalanceService);
    }

    [Fact]
    public async Task GetThirteenthMonthAsync_WithEmployeeId_OnlyReturnsThatEmployeesRow()
    {
        var mine = BuildPayroll(Guid.NewGuid(), 2026, 120_000);
        var others = BuildPayroll(Guid.NewGuid(), 2026, 240_000);
        var service = BuildService(mine, others);

        var result = await service.GetThirteenthMonthAsync(2026, CancellationToken.None, employeeId: mine.EmployeeId);

        result.Should().ContainSingle();
        result[0].EmployeeId.Should().Be(mine.EmployeeId);
        result[0].ThirteenthMonthPay.Should().Be(120_000m / 12);
    }

    [Fact]
    public async Task GetThirteenthMonthAsync_WithoutEmployeeId_ReturnsEveryEmployee()
    {
        var a = BuildPayroll(Guid.NewGuid(), 2026, 120_000);
        var b = BuildPayroll(Guid.NewGuid(), 2026, 240_000);
        var service = BuildService(a, b);

        var result = await service.GetThirteenthMonthAsync(2026, CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetThirteenthMonthAsync_WithClientId_OnlyReturnsThatClientsEmployees()
    {
        var client = Guid.NewGuid();
        var inClient = BuildPayroll(Guid.NewGuid(), 2026, 120_000);
        var otherClient = BuildPayroll(Guid.NewGuid(), 2026, 240_000);
        var noClient = BuildPayroll(Guid.NewGuid(), 2026, 360_000);
        var service = BuildService(
            new Dictionary<Guid, Guid> { [inClient.EmployeeId] = client, [otherClient.EmployeeId] = Guid.NewGuid() },
            inClient, otherClient, noClient);

        var result = await service.GetThirteenthMonthAsync(2026, CancellationToken.None, clientId: client);

        result.Should().ContainSingle();
        result[0].EmployeeId.Should().Be(inClient.EmployeeId);
    }

    [Fact]
    public async Task GetBankDisbursementAsync_WithClientId_OnlyReturnsThatClientsEmployees()
    {
        var client = Guid.NewGuid();
        var inClient = BuildPayroll(Guid.NewGuid(), 2026, 120_000);
        var otherClient = BuildPayroll(Guid.NewGuid(), 2026, 240_000);
        var service = BuildService(
            new Dictionary<Guid, Guid> { [inClient.EmployeeId] = client, [otherClient.EmployeeId] = Guid.NewGuid() },
            inClient, otherClient);

        var filtered = await service.GetBankDisbursementAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), CancellationToken.None, client);
        var everyone = await service.GetBankDisbursementAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), CancellationToken.None);

        filtered.Should().ContainSingle().Which.EmployeeId.Should().Be(inClient.EmployeeId);
        everyone.Should().HaveCount(2);
    }
}
