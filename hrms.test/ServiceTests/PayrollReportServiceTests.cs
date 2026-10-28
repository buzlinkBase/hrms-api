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
        var employees = payrolls.Select(p => p.EmployeeId).Distinct().Select(id =>
        {
            var employee = BuildEmployee(id, id.ToString()[..8]);
            if (clients != null && clients.TryGetValue(id, out var clientId)) employee.ClientId = clientId;
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

    // The 13th month payout row "Generate 13th Month" creates: a snapshot of the figure that day.
    private static Payroll BuildThirteenthMonthPayout(Guid employeeId, int year, decimal gross, decimal netPay, bool posted = true) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        PostingPeriod = new DateOnly(year, 10, 15),
        PayPeriodStart = new DateOnly(year, 1, 1),
        PayPeriodEnd = new DateOnly(year, 12, 31),
        IsPosted = posted,
        PayrollType = PayrollType.ThirteenthMonth,
        GrossIncome = gross,
        NetPay = netPay,
    };

    [Fact]
    public async Task APayoutGeneratedBeforeLaterCutoffs_LeavesTheDifferenceStillUnpaid()
    {
        // Generated when basic pay so far was 7,532.40 (-> 627.70); a later cutoff brought it to 14,067.24.
        var employee = Guid.NewGuid();
        var early = BuildPayroll(employee, 2026, 7_532.40m);
        var later = BuildPayroll(employee, 2026, 6_534.84m);
        later.Id = Guid.NewGuid();
        var payout = BuildThirteenthMonthPayout(employee, 2026, gross: 627.70m, netPay: 627.70m);
        var service = BuildService(early, later, payout);

        var row = (await service.GetThirteenthMonthAsync(2026, CancellationToken.None)).Single();

        row.ThirteenthMonthPay.Should().Be(14_067.24m / 12);
        row.Status.Should().Be("Posted");
        row.PaidGross.Should().Be(627.70m);
        row.NetPay.Should().Be(627.70m);
        row.StillUnpaid.Should().Be(544.57m);
    }

    [Fact]
    public async Task APayoutThatCoversEverythingEarned_LeavesNothingUnpaid_EvenWhenTaxWasWithheld()
    {
        var employee = Guid.NewGuid();
        var payroll = BuildPayroll(employee, 2026, 1_200_000m);
        // 100,000 gross; tax withheld on the part above the ceiling, so the net is lower.
        var payout = BuildThirteenthMonthPayout(employee, 2026, gross: 100_000m, netPay: 97_500m);
        var service = BuildService(payroll, payout);

        var row = (await service.GetThirteenthMonthAsync(2026, CancellationToken.None)).Single();

        row.StillUnpaid.Should().Be(0, "the payout's gross, not its after-tax net, is compared");
        row.NetPay.Should().Be(97_500m);
    }

    [Fact]
    public async Task WithoutAPayout_NothingIsPaid_AndNothingIsStillUnpaid()
    {
        var service = BuildService(BuildPayroll(Guid.NewGuid(), 2026, 120_000));

        var row = (await service.GetThirteenthMonthAsync(2026, CancellationToken.None)).Single();

        row.Status.Should().Be("NotGenerated");
        row.PaidGross.Should().BeNull();
        row.StillUnpaid.Should().Be(0);
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
