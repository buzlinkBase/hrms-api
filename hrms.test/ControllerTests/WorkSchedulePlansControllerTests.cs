using System.Linq.Expressions;
using System.Security.Claims;
using Hrms.Api.Controllers;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ControllerTests;

/// <summary>
/// WorkSchedulePlansController.ValidateTeamScopeAsync -- the server-side enforcement behind
/// Work Rotation's ManageOwnTeam permission (see Hrms.Core.Services.EmployeeService.Filter's
/// ManagerId param). Holding Work Rotation:Create (or neither permission -- today's default for
/// every non-Owner role, since nothing has ever gated this endpoint) must behave exactly as
/// before: unscoped, no rejection. Only a caller holding ManageOwnTeam WITHOUT Create gets
/// restricted to their own direct reports (Employee.ManagerId).
///
/// Tested directly against ValidateTeamScopeAsync (internal, takes an explicit ClaimsPrincipal)
/// rather than through the full PostBatch action -- PostBatch's own persistence step
/// (WorkSchedulePlanService.AddRange) calls ExecuteDeleteAsync, which needs a real relational
/// provider and can't run against this project's usual mocked-repository queryables.
/// </summary>
public class WorkSchedulePlansControllerTests
{
    private static ClaimsPrincipal BuildUser(Guid userId, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("email", "caller@company.com"),
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    private static WorkSchedulePlansController BuildController(params Employee[] employees)
    {
        var repo = Substitute.For<IRepository>();
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

        return new WorkSchedulePlansController(
            new WorkSchedulePlanService(uow, TypeAdapterConfig.GlobalSettings, Substitute.For<IMapper>()),
            employeeService,
            Substitute.For<IMapper>());
    }

    // EmployeeService.Filter projects straight to EmployeeFilterResponseModel via
    // Employee.PayrollGroup.Name / .Client.Name / .Branch.Name / .Department.Name / .Area.Name
    // with no null-guard (unlike EmployeeModel/EmployeeFullModel's mappings) -- these navigation
    // properties must be non-null or the projection throws NullReferenceException.
    private static Employee BuildEmployee(Guid? userId = null, Guid? managerId = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        ManagerId = managerId,
        PayrollGroup = new PayrollGroup(),
        Client = new Client(),
        Branch = new Branch(),
        Department = new Department { Employees = [] },
        Area = new CostCenters(),
        RestDays = [],
        FixedSchedule = [],
    };

    [Fact]
    public async Task ValidateTeamScopeAsync_AllowsAnyEmployee_WhenCallerHasCreate()
    {
        var controller = BuildController();
        var user = BuildUser(Guid.NewGuid(), "Work Rotation:Create");

        var result = await controller.ValidateTeamScopeAsync(user, [Guid.NewGuid()], CancellationToken.None);

        result.Should().BeNull("Create is unscoped -- any employeeId is accepted, exactly like before this permission existed");
    }

    [Fact]
    public async Task ValidateTeamScopeAsync_AllowsAnyEmployee_WhenCallerHasNeitherPermission()
    {
        var controller = BuildController();
        var user = BuildUser(Guid.NewGuid());

        var result = await controller.ValidateTeamScopeAsync(user, [Guid.NewGuid()], CancellationToken.None);

        result.Should().BeNull("this is today's default for every non-Owner role -- introducing ManageOwnTeam must never retroactively lock this out");
    }

    [Fact]
    public async Task ValidateTeamScopeAsync_Allows_WhenEveryEmployeeIsACallerDirectReport()
    {
        var callerUserId = Guid.NewGuid();
        var caller = BuildEmployee(userId: callerUserId);
        var report1 = BuildEmployee(managerId: caller.Id);
        var report2 = BuildEmployee(managerId: caller.Id);
        var controller = BuildController(caller, report1, report2);
        var user = BuildUser(callerUserId, "Work Rotation:ManageOwnTeam");

        var result = await controller.ValidateTeamScopeAsync(
            user, [report1.Id, report2.Id], CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTeamScopeAsync_Forbids_WhenAnEmployeeIsNotACallerDirectReport()
    {
        var callerUserId = Guid.NewGuid();
        var caller = BuildEmployee(userId: callerUserId);
        var myReport = BuildEmployee(managerId: caller.Id);
        var someoneElsesReport = BuildEmployee(managerId: Guid.NewGuid());
        var controller = BuildController(caller, myReport, someoneElsesReport);
        var user = BuildUser(callerUserId, "Work Rotation:ManageOwnTeam");

        var result = await controller.ValidateTeamScopeAsync(
            user, [myReport.Id, someoneElsesReport.Id], CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ValidateTeamScopeAsync_Forbids_WhenCallerHasNoLinkedEmployeeRecord()
    {
        var controller = BuildController();
        var user = BuildUser(Guid.NewGuid(), "Work Rotation:ManageOwnTeam");

        var result = await controller.ValidateTeamScopeAsync(user, [Guid.NewGuid()], CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }
}
