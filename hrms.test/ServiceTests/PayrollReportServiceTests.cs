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

    private static PayrollReportService BuildService(params Payroll[] payrolls)
    {
        var employees = payrolls.Select(p => BuildEmployee(p.EmployeeId, p.EmployeeId.ToString()[..8])).ToList();

        var repo = Substitute.For<IRepository>();
        repo.FindAll<Payroll>().Returns(_ => payrolls.ToList().BuildMockDbSet());
        repo.FindAll<PayrollOpeningBalance>().Returns(_ => new List<PayrollOpeningBalance>().BuildMockDbSet());
        repo.FindAll<Employee>().Returns(_ => employees.ToList().BuildMockDbSet());

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

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
}
