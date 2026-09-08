using System.Security.Claims;
using Hrms.Api.Controllers;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ControllerTests;

/// <summary>
/// MeController — the self-service portal's ownership guard. Every action resolves the
/// caller's own EmployeeId server-side from the JWT and must never leak another employee's
/// data, regardless of what payrollId is requested. These tests cover that guard directly
/// (PrintMyPayslip's 404-on-mismatch and 404-on-unlinked-user paths) rather than the
/// pre-existing PayslipDocument rendering, which is unchanged.
/// </summary>
public class MeControllerTests
{
    private static Employee BuildEmployee(Guid userId, string employeeNo = "EMP-001") => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        EmployeeNo = employeeNo,
        Skills = [],
        Educations = [],
        Dependents = [],
        EmployeeRecords = [],
        Employments = [],
        Assets = [],
        RestDays = [],
    };

    private static Payroll BuildPayroll(Guid employeeId) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        PayPeriodStart = new DateOnly(2026, 9, 1),
        PayPeriodEnd = new DateOnly(2026, 9, 15),
    };

    private static (MeController Controller, Guid CallerUserId) BuildController(Employee? caller, Payroll? payroll)
    {
        var repo = Substitute.For<IRepository>();
        var employees = caller is null ? Array.Empty<Employee>() : [caller];
        // BuildMockDbSet() itself uses NSubstitute internally, so it must be deferred inside
        // Returns(callInfo => ...) — see EmployeeServiceTests.SeedRepo for the full explanation.
        repo.FindAll<Employee>().Returns(_ => employees.ToList().BuildMockDbSet());
        repo.FindAll<Company>().Returns(_ => new List<Company>().BuildMockDbSet());
        repo.FindOneAsync<Payroll>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => payroll != null && call.Arg<Guid>() == payroll.Id ? payroll : null);

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
        var payrollService = new PayrollService(uow, Substitute.For<IMapper>());
        var companyService = new CompanyService(uow);

        var callerUserId = caller?.UserId ?? Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, callerUserId.ToString()),
            new Claim("email", "caller@company.com"),
        ]));

        var controller = new MeController(employeeService, payrollService, companyService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

        return (controller, callerUserId);
    }

    [Fact]
    public async Task PrintMyPayslip_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null, payroll: null);

        var result = await controller.PrintMyPayslip(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PrintMyPayslip_PayrollBelongsToSomeoneElse_ReturnsNotFound()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var someoneElsesPayroll = BuildPayroll(Guid.NewGuid());
        var (controller, _) = BuildController(caller, someoneElsesPayroll);

        var result = await controller.PrintMyPayslip(someoneElsesPayroll.Id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PrintMyPayslip_UnknownPayrollId_ReturnsNotFound()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var (controller, _) = BuildController(caller, payroll: null);

        var result = await controller.PrintMyPayslip(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }
}
