using System.Linq.Expressions;
using System.Security.Claims;
using Hrms.Api.Controllers;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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

    // GenderRestriction.None + AllowHalfDay=true + AllowNegativeBalance=true is enough for
    // EnsurePolicyAsync to pass without also needing to seed an Employee (gender check) or
    // LeaveCredits (balance check) lookup.
    private static Leave BuildLeave() => new()
    {
        Id = Guid.NewGuid(),
        Code = "VL",
        Description = "Vacation Leave",
        AllowHalfDay = true,
        AllowNegativeBalance = true,
    };

    private static LeaveApplication BuildLeaveApplication(Guid employeeId, Leave leave) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        LeaveId = leave.Id,
        Leave = leave,
        ApprovalStatus = ApprovalStatus.ForApproval,
    };

    private static OverTimeApplication BuildOvertimeApplication(Guid employeeId) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        Employee = BuildEmployee(Guid.NewGuid()),
        ApprovalStatus = ApprovalStatus.ForApproval,
    };

    private static TravelOrderApplication BuildTravelOrderApplication(Guid employeeId) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        ApprovalStatus = ApprovalStatus.ForApproval,
    };

    private static PassSlipApplication BuildPassSlipApplication(Guid employeeId) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        ApprovalStatus = ApprovalStatus.ForApproval,
    };

    // FindList groups by (BatchCode, EmployeeId, Employee.FirstName, Employee.LastName), so
    // Employee must be populated to avoid a null-reference during the in-memory GroupBy.
    private static ChangeRestDay BuildChangeRestDay(Guid employeeId, string batchCode) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        Employee = BuildEmployee(Guid.NewGuid()),
        BatchCode = batchCode,
        PayrollDate = new DateOnly(2026, 9, 6),
        DayName = DayName.Sunday,
        State = ChangeSchedState.REPLACEMENT,
        ApprovalStatus = ApprovalStatus.ForApproval,
    };

    private static Payroll BuildRegularPayroll(Guid employeeId, int year, decimal basicPay) => new()
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

    private static DeductionApplication BuildDeductionApplication(Guid employeeId) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = employeeId,
        Breakdown = [],
        ApprovalStatus = ApprovalStatus.ForApproval,
    };

    private static (MeController Controller, Guid CallerUserId) BuildController(
        Employee? caller,
        Payroll? payroll = null,
        EmployeeFixedSchedule[]? fixedSchedules = null,
        Leave[]? leaves = null,
        LeaveApplication[]? leaveApplications = null,
        OverTimeApplication[]? overtimeApplications = null,
        TravelOrderApplication[]? travelOrderApplications = null,
        ChangeRestDay[]? changeRestDays = null,
        DeductionApplication[]? deductionApplications = null,
        Payroll[]? payrollRows = null,
        UserRole[]? userRoles = null,
        RolePermission[]? rolePermissions = null)
    {
        var repo = Substitute.For<IRepository>();
        var employees = caller is null ? Array.Empty<Employee>() : [caller];
        var schedules = fixedSchedules ?? [];
        var leaveTypes = leaves ?? [];
        var applications = leaveApplications ?? [];
        var overtimeApps = overtimeApplications ?? [];
        var travelOrderApps = travelOrderApplications ?? [];
        var changeRestDayRows = changeRestDays ?? [];
        var deductionApps = deductionApplications ?? [];
        var payrollRowsList = payrollRows ?? [];
        var userRoleRows = userRoles ?? [];
        var rolePermissionRows = rolePermissions ?? [];
        // BuildMockDbSet() itself uses NSubstitute internally, so it must be deferred inside
        // Returns(callInfo => ...) — see EmployeeServiceTests.SeedRepo for the full explanation.
        repo.FindAll<Employee>().Returns(_ => employees.ToList().BuildMockDbSet());
        repo.FindAll<Company>().Returns(_ => new List<Company>().BuildMockDbSet());
        repo.FindAll<EmployeeFixedSchedule>().Returns(_ => schedules.ToList().BuildMockDbSet());
        repo.FindAll<LeaveApplication>().Returns(_ => applications.ToList().BuildMockDbSet());
        repo.FindAll<OverTimeApplication>().Returns(_ => overtimeApps.ToList().BuildMockDbSet());
        repo.FindAll<TravelOrderApplication>().Returns(_ => travelOrderApps.ToList().BuildMockDbSet());
        repo.FindAll<ChangeRestDay>().Returns(_ => changeRestDayRows.ToList().BuildMockDbSet());
        repo.FindAll<DeductionApplication>().Returns(_ => deductionApps.ToList().BuildMockDbSet());
        repo.FindAll<Payroll>().Returns(_ => payrollRowsList.ToList().BuildMockDbSet());
        repo.FindAll<PayrollOpeningBalance>().Returns(_ => new List<PayrollOpeningBalance>().BuildMockDbSet());
        repo.Find<Leave>(Arg.Any<Expression<Func<Leave, bool>>>())
            .Returns(call => leaveTypes.Where(call.Arg<Expression<Func<Leave, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<UserRole>(Arg.Any<Expression<Func<UserRole, bool>>>())
            .Returns(call => userRoleRows.Where(call.Arg<Expression<Func<UserRole, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<RolePermission>(Arg.Any<Expression<Func<RolePermission, bool>>>())
            .Returns(call => rolePermissionRows.Where(call.Arg<Expression<Func<RolePermission, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.FindOneAsync<Payroll>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => payroll != null && call.Arg<Guid>() == payroll.Id ? payroll : null);

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
        var payrollService = new PayrollService(uow, Substitute.For<IMapper>());
        var companyService = new CompanyService(uow);
        var fixedScheduleService = new EmployeeFixedScheduleService(uow);
        var leaveLedgerService = new LeaveLedgerService(uow);
        // Bare IMapper substitute: LeaveApplicationService.AddAsync's Map<LeaveApplication>()
        // call returns null with it, which short-circuits AddAsync before it ever persists —
        // fine for these tests, which only assert on the EmployeeId/ApprovalStatus forced onto
        // the payload BEFORE AddAsync is called, not on what (if anything) gets saved.
        var leaveApplicationService = new LeaveApplicationService(
            uow, TypeAdapterConfig.GlobalSettings, Substitute.For<IMapper>(),
            Substitute.For<IPublishEndpoint>(), Substitute.For<ILogger<LeaveApplicationService>>());
        var overtimeApplicationService = new OvertimeApplicationService(uow);
        var travelOrderApplicationService = new TravelOrderApplicationService(uow);
        var passSlipApplicationService = new PassSlipApplicationService(uow, new AttendanceService(uow));
        var changeRestDayService = new ChangeRestDayService(uow);
        var deductionApplicationService = new DeductionApplicationService(
            uow, new DeductionService(uow), employeeService, Substitute.For<IMapper>());
        var payrollReportService = new PayrollReportService(
            uow, employeeService, new PayrollOpeningBalanceService(uow));
        var userRoleService = new UserRoleService(uow);

        // Unlike Leave's bare mapper above, Overtime/TravelOrder/PassSlip's controller actions
        // map to a NEW entity themselves (their services take the entity, not the raw DTO), so
        // the mapper here needs to actually map — real Mapster Adapt() via the scanned global
        // config, not a stub — otherwise every Create*Application test would see a null entity.
        var mapper = Substitute.For<IMapper>();
        mapper.Map<OverTimeApplication>(Arg.Any<object>())
            .Returns(call => ((CreateOverTimeApplication)call.Arg<object>()).Adapt<OverTimeApplication>());
        mapper.Map<TravelOrderApplication>(Arg.Any<object>())
            .Returns(call => ((CreateTravelOrderApplication)call.Arg<object>()).Adapt<TravelOrderApplication>());
        mapper.Map<PassSlipApplication>(Arg.Any<object>())
            .Returns(call => ((CreatePassSlipApplication)call.Arg<object>()).Adapt<PassSlipApplication>());
        mapper.Map<List<DeductionApplicationModel>>(Arg.Any<object>())
            .Returns(call => ((List<DeductionApplication>)call.Arg<object>()).Adapt<List<DeductionApplicationModel>>());

        var callerUserId = caller?.UserId ?? Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, callerUserId.ToString()),
            new Claim("email", "caller@company.com"),
        ]));

        // DTRCalcService is never used by the no-linked-employee guard tests (they 404 before
        // it's touched) and its full pipeline is impractical to construct here — see class doc.
        var controller = new MeController(
            employeeService, payrollService, companyService, null!, fixedScheduleService,
            leaveLedgerService, leaveApplicationService, overtimeApplicationService,
            travelOrderApplicationService, passSlipApplicationService, changeRestDayService,
            deductionApplicationService, payrollReportService, userRoleService, mapper)
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

    [Fact]
    public async Task GetMyLeaveCredits_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);

        var result = await controller.GetMyLeaveCredits(2026, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyLeaveApplications_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);

        var result = await controller.GetMyLeaveApplications(CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyLeaveApplications_ReturnsOnlyCallersOwnApplications()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var someoneElse = BuildEmployee(Guid.NewGuid());
        var leave = BuildLeave();
        var myApplication = BuildLeaveApplication(caller.Id, leave);
        var othersApplication = BuildLeaveApplication(someoneElse.Id, leave);
        var (controller, _) = BuildController(caller, leaveApplications: [myApplication, othersApplication]);

        var result = await controller.GetMyLeaveApplications(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var applications = ok.Value.Should().BeAssignableTo<List<LeaveApplicationModel>>().Subject;
        applications.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateMyLeaveApplication_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);
        var payload = new CreateLeaveApplication { LeaveId = Guid.NewGuid() };

        var result = await controller.CreateMyLeaveApplication(payload, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateMyLeaveApplication_ForcesCallersOwnEmployeeIdAndForApprovalStatus()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var leave = BuildLeave();
        var (controller, _) = BuildController(caller, leaves: [leave]);
        // A malicious/buggy client tries to file under someone else's identity, pre-approved.
        var payload = new CreateLeaveApplication
        {
            LeaveId = leave.Id,
            EmployeeId = Guid.NewGuid(),
            ApprovalStatus = ApprovalStatus.Approved,
        };

        var result = await controller.CreateMyLeaveApplication(payload, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        payload.EmployeeId.Should().Be(caller.Id);
        payload.ApprovalStatus.Should().Be(ApprovalStatus.ForApproval);
    }

    [Fact]
    public async Task GetMyOvertimeApplications_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);

        var result = await controller.GetMyOvertimeApplications(CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyOvertimeApplications_ReturnsOnlyCallersOwnApplications()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var someoneElse = BuildEmployee(Guid.NewGuid());
        var mine = BuildOvertimeApplication(caller.Id);
        var others = BuildOvertimeApplication(someoneElse.Id);
        var (controller, _) = BuildController(caller, overtimeApplications: [mine, others]);

        var result = await controller.GetMyOvertimeApplications(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var applications = ok.Value.Should().BeAssignableTo<List<OverTimeApplication>>().Subject;
        applications.Should().ContainSingle();
        applications[0].EmployeeId.Should().Be(caller.Id);
    }

    [Fact]
    public async Task CreateMyOvertimeApplication_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);
        var payload = new CreateOverTimeApplication { OTDate = new DateOnly(2026, 9, 9) };

        var result = await controller.CreateMyOvertimeApplication(payload, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateMyOvertimeApplication_ForcesCallersOwnEmployeeId()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var (controller, _) = BuildController(caller);
        // A malicious/buggy client tries to file under someone else's identity.
        var payload = new CreateOverTimeApplication
        {
            OTDate = new DateOnly(2026, 9, 9),
            EmployeeId = Guid.NewGuid(),
        };

        var result = await controller.CreateMyOvertimeApplication(payload, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        payload.EmployeeId.Should().Be(caller.Id);
    }

    [Fact]
    public async Task GetMyTravelOrderApplications_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);

        var result = await controller.GetMyTravelOrderApplications(CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyTravelOrderApplications_ReturnsOnlyCallersOwnApplications()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var someoneElse = BuildEmployee(Guid.NewGuid());
        var mine = BuildTravelOrderApplication(caller.Id);
        var others = BuildTravelOrderApplication(someoneElse.Id);
        var (controller, _) = BuildController(caller, travelOrderApplications: [mine, others]);

        var result = await controller.GetMyTravelOrderApplications(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var applications = ok.Value.Should().BeAssignableTo<List<TravelOrderApplication>>().Subject;
        applications.Should().ContainSingle();
        applications[0].EmployeeId.Should().Be(caller.Id);
    }

    [Fact]
    public async Task CreateMyTravelOrderApplication_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);
        var payload = new CreateTravelOrderApplication { StartDate = new DateOnly(2026, 9, 9), EndDate = new DateOnly(2026, 9, 9) };

        var result = await controller.CreateMyTravelOrderApplication(payload, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateMyTravelOrderApplication_ForcesCallersOwnEmployeeIdAndZeroCost()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var (controller, _) = BuildController(caller);
        // A malicious/buggy client tries to file under someone else's identity with a padded cost.
        var payload = new CreateTravelOrderApplication
        {
            StartDate = new DateOnly(2026, 9, 9),
            EndDate = new DateOnly(2026, 9, 9),
            EmployeeId = Guid.NewGuid(),
            Cost = 5000,
        };

        var result = await controller.CreateMyTravelOrderApplication(payload, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        payload.EmployeeId.Should().Be(caller.Id);
        payload.Cost.Should().Be(0);
    }

    [Fact]
    public async Task GetMyPassSlipApplications_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);

        var result = await controller.GetMyPassSlipApplications(CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateMyPassSlipApplication_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);
        var payload = new CreatePassSlipApplication { ApplicationDate = new DateOnly(2026, 9, 9), DepartureTime = DateTime.UtcNow, Remarks = "Bank errand" };

        var result = await controller.CreateMyPassSlipApplication(payload, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateMyPassSlipApplication_ForcesCallersOwnEmployeeId()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var (controller, _) = BuildController(caller);
        // A malicious/buggy client tries to file under someone else's identity.
        var payload = new CreatePassSlipApplication
        {
            ApplicationDate = new DateOnly(2026, 9, 9),
            DepartureTime = DateTime.UtcNow,
            Remarks = "Bank errand",
            EmployeeId = Guid.NewGuid(),
        };

        var result = await controller.CreateMyPassSlipApplication(payload, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        payload.EmployeeId.Should().Be(caller.Id);
    }

    [Fact]
    public async Task GetMyChangeRestDayRequests_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);

        var result = await controller.GetMyChangeRestDayRequests(CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyChangeRestDayRequests_ReturnsOnlyCallersOwnRequests()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var someoneElse = BuildEmployee(Guid.NewGuid());
        var mine = BuildChangeRestDay(caller.Id, "COFF-MINE");
        var others = BuildChangeRestDay(someoneElse.Id, "COFF-OTHERS");
        var (controller, _) = BuildController(caller, changeRestDays: [mine, others]);

        var result = await controller.GetMyChangeRestDayRequests(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var requests = ok.Value.Should().BeAssignableTo<List<RestDayRecordResponse>>().Subject;
        requests.Should().ContainSingle();
        requests[0].EmployeeId.Should().Be(caller.Id);
    }

    [Fact]
    public async Task CreateMyChangeRestDayRequest_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);
        var payload = new RequestChangeRestDay
        {
            FromDay = DayName.Sunday,
            ToDay = DayName.Monday,
            PayrollDateFrom = new DateOnly(2026, 9, 6),
            PayrollDateTo = new DateOnly(2026, 9, 7),
        };

        var result = await controller.CreateMyChangeRestDayRequest(payload, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyLoanApplications_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);

        var result = await controller.GetMyLoanApplications(CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyLoanApplications_ReturnsOnlyCallersOwnApplications()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var someoneElse = BuildEmployee(Guid.NewGuid());
        var mine = BuildDeductionApplication(caller.Id);
        var others = BuildDeductionApplication(someoneElse.Id);
        var (controller, _) = BuildController(caller, deductionApplications: [mine, others]);

        var result = await controller.GetMyLoanApplications(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var applications = ok.Value.Should().BeAssignableTo<List<DeductionApplicationModel>>().Subject;
        applications.Should().ContainSingle();
        applications[0].EmployeeId.Should().Be(caller.Id);
    }

    [Fact]
    public async Task CreateMyLoanApplication_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);
        var payload = new CreateDeductionApplication
        {
            DeductionId = Guid.NewGuid(),
            StartDate = new DateOnly(2026, 9, 9),
            EndDate = new DateOnly(2026, 12, 9),
            Breakdown = [],
        };

        var result = await controller.CreateMyLoanApplication(payload, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMy13thMonth_NoLinkedEmployee_ReturnsNotFound()
    {
        var (controller, _) = BuildController(caller: null);

        var result = await controller.GetMy13thMonth(2026, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMy13thMonth_ReturnsOnlyCallersOwnFigure()
    {
        var caller = BuildEmployee(Guid.NewGuid());
        var someoneElse = BuildEmployee(Guid.NewGuid());
        var mine = BuildRegularPayroll(caller.Id, 2026, 120_000);
        var others = BuildRegularPayroll(someoneElse.Id, 2026, 240_000);
        var (controller, _) = BuildController(caller, payrollRows: [mine, others]);

        var result = await controller.GetMy13thMonth(2026, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var model = ok.Value.Should().BeOfType<ThirteenthMonthModel>().Subject;
        model.EmployeeId.Should().Be(caller.Id);
        model.ThirteenthMonthPay.Should().Be(120_000m / 12);
    }

    [Fact]
    public async Task GetMyPermissions_ReturnsUnionAcrossAllAssignedRoles()
    {
        var callerUserId = Guid.NewGuid();
        var caller = BuildEmployee(callerUserId);
        var roleA = Guid.NewGuid();
        var roleB = Guid.NewGuid();
        var shared = new Permission { Id = Guid.NewGuid(), Code = "PayrollRun:View" };
        var onlyA = new Permission { Id = Guid.NewGuid(), Code = "PayrollRun:Approve" };
        var onlyB = new Permission { Id = Guid.NewGuid(), Code = "Users:Manage" };
        var (controller, _) = BuildController(
            caller,
            userRoles: [
                new UserRole { UserId = callerUserId, RoleId = roleA },
                new UserRole { UserId = callerUserId, RoleId = roleB },
            ],
            rolePermissions: [
                new RolePermission { RoleId = roleA, PermissionId = shared.Id, Permission = shared },
                new RolePermission { RoleId = roleA, PermissionId = onlyA.Id, Permission = onlyA },
                new RolePermission { RoleId = roleB, PermissionId = shared.Id, Permission = shared },
                new RolePermission { RoleId = roleB, PermissionId = onlyB.Id, Permission = onlyB },
            ]);

        var result = await controller.GetMyPermissions(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var codes = ok.Value.Should().BeAssignableTo<List<string>>().Subject;
        codes.Should().BeEquivalentTo(["PayrollRun:View", "PayrollRun:Approve", "Users:Manage"]);
    }
}
