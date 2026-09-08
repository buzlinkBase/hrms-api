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
/// data, regardless of what payrollId/date-range is requested. Most tests cover the
/// no-linked-employee 404 path shared by every action; GetMyFixedSchedule additionally gets a
/// positive-path test since EmployeeFixedScheduleService is cheap to build for real (unlike
/// DTRCalcService, whose full pipeline is far too deep to construct just to prove a guard
/// clause that runs before it's ever touched — see the `null!` dtrCalcService below).
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

    private static (MeController Controller, Guid CallerUserId) BuildController(
        Employee? caller, Payroll? payroll = null, EmployeeFixedSchedule[]? fixedSchedules = null)
    {
        var repo = Substitute.For<IRepository>();
        var employees = caller is null ? Array.Empty<Employee>() : [caller];
        var schedules = fixedSchedules ?? [];
        // BuildMockDbSet() itself uses NSubstitute internally, so it must be deferred inside
        // Returns(callInfo => ...) — see EmployeeServiceTests.SeedRepo for the full explanation.
        repo.FindAll<Employee>().Returns(_ => employees.ToList().BuildMockDbSet());
        repo.FindAll<Company>().Returns(_ => new List<Company>().BuildMockDbSet());
        repo.FindAll<EmployeeFixedSchedule>().Returns(_ => schedules.ToList().BuildMockDbSet());
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
        var fixedScheduleService = new EmployeeFixedScheduleService(uow);

        var callerUserId = caller?.UserId ?? Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, callerUserId.ToString()),
            new Claim("email", "caller@company.com"),
        ]));

        // DTRCalcService is never used by the no-linked-employee guard tests (they 404 before
        // it's touched) and its full pipeline is impractical to construct here — see class doc.
        var controller = new MeController(employeeService, payrollService, companyService, null!, fixedScheduleService)
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

    [Fact]
    public async Task GetMyDtrDetail_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);

        var result = await controller.GetMyDtrDetail(
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 15), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyIncompletePunches_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);

        var result = await controller.GetMyIncompletePunches(
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 15), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyFixedSchedule_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);

        var result = await controller.GetMyFixedSchedule(CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyFixedSchedule_ReturnsOnlyCallersOwnSchedule()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var someoneElse = BuildEmployee(Guid.NewGuid());
        var timeShift = new TimeShift { Id = Guid.NewGuid(), ShiftName = "Day Shift", StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) };
        var mySchedule = new EmployeeFixedSchedule { Id = Guid.NewGuid(), EmployeeId = caller.Id, DayName = DayName.Monday, TimeShiftId = timeShift.Id, TimeShift = timeShift };
        var othersSchedule = new EmployeeFixedSchedule { Id = Guid.NewGuid(), EmployeeId = someoneElse.Id, DayName = DayName.Monday, TimeShiftId = timeShift.Id, TimeShift = timeShift };
        var (controller, _) = BuildController(caller, fixedSchedules: [mySchedule, othersSchedule]);

        var result = await controller.GetMyFixedSchedule(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var schedule = ok.Value.Should().BeAssignableTo<List<EmployeeFixedScheduleModel>>().Subject;
        schedule.Should().ContainSingle();
        schedule[0].EmployeeId.Should().Be(caller.Id);
        schedule[0].TimeShiftName.Should().Be("Day Shift");
    }
}
