using System.Security.Claims;
using Hrms.Api.Extensions;
using Hrms.Core.Services.Security;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.InfrastructureTests;

/// <summary>
/// Confidential employees: the global query filter (ConfidentialityFilters, fed per request by
/// ConfidentialAccessScope) hides a confidential employee -- and everything about them -- from
/// callers without the "Confidential Records:Access" clearance, while cleared callers, background
/// work and the employee themself see everything. An assigned approver sees just that request.
/// </summary>
public class ConfidentialAccessTests
{
    private static readonly Guid CallerUserId = Guid.NewGuid();

    internal sealed record Seeded(
        Guid PayrollGroupId, Guid Public, Guid Secret, Guid Caller,
        Guid SecretLeaveApplication, Guid SecretDailyRecord);

    // A public employee, a confidential one (with a DTR row, leave credits, a dependent and a
    // leave application) and the signed-in caller's own (non-confidential) employee record.
    internal static async Task<Seeded> SeedAsync(SqliteHrmsContext db, bool secretIsCaller = false)
    {
        await using var seed = db.NewContext();
        var payrollGroup = new PayrollGroup { Id = Guid.NewGuid(), Name = "Main" };
        seed.PayrollGroups.Add(payrollGroup);

        var publicEmployee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroup.Id, JobLevel = "", FirstName = "Pat", LastName = "Public", BioId = 1 };
        var secret = new Employee
        {
            Id = Guid.NewGuid(), PayrollGroupId = payrollGroup.Id, JobLevel = "", FirstName = "Sam", LastName = "Secret", BioId = 2,
            Email = "sam@example.com", IsConfidential = true, UserId = secretIsCaller ? CallerUserId : null,
        };
        var caller = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroup.Id, JobLevel = "", FirstName = "Cal", LastName = "Caller", BioId = 3, UserId = secretIsCaller ? null : CallerUserId };
        seed.Employees.AddRange(publicEmployee, secret, caller);

        var leave = new Leave { Id = Guid.NewGuid(), Code = "VL", Description = "Vacation Leave" };
        seed.Leaves.Add(leave);
        seed.LeaveCredits.Add(new LeaveCredits
        {
            Id = Guid.NewGuid(), EmployeeId = secret.Id, LeaveId = leave.Id, PeriodYear = 2026,
            FromDate = new DateTime(2026, 1, 1), ToDate = new DateTime(2026, 12, 31), Granted = 10m, Balance = 10m,
        });
        var application = new LeaveApplication
        {
            Id = Guid.NewGuid(), EmployeeId = secret.Id, LeaveId = leave.Id, Leave = null!,
            DurationType = DurationType.MultiDay, LeaveDateFrom = new DateOnly(2026, 3, 2), LeaveDateTo = new DateOnly(2026, 3, 3),
            ApprovalStatus = ApprovalStatus.ForApproval,
        };
        seed.leaveApplications.Add(application);
        var dailyRecord = new DailyRecord { Id = Guid.NewGuid(), EmployeeId = secret.Id, WorkDate = new DateOnly(2026, 3, 1), BatchCode = "B1" };
        seed.DailyTimeRecords.AddRange(
            dailyRecord,
            new DailyRecord { Id = Guid.NewGuid(), EmployeeId = publicEmployee.Id, WorkDate = new DateOnly(2026, 3, 1), BatchCode = "B1" });
        seed.Dependents.Add(new Dependent { Id = Guid.NewGuid(), EmployeeId = secret.Id, FullName = "Kid", Relationship = "Child", Gender = "Female" });

        await seed.SaveChangesAsync();
        return new Seeded(payrollGroup.Id, publicEmployee.Id, secret.Id, caller.Id, application.Id, dailyRecord.Id);
    }

    internal static async Task<ConfidentialAccessScope> ScopeForCallerAsync(
        SqliteHrmsContext db, string? email = null, ConfidentialGrants grants = default)
    {
        var scope = new ConfidentialAccessScope();
        await using var context = db.NewContext();
        await scope.InitializeAsync(context, Guid.Empty, CallerUserId, email, grants, CancellationToken.None);
        return scope;
    }

    // The caller is the Person approver of step 1 of a Leave workflow the secret employee's
    // application is routed through.
    private static async Task<Guid> SeedApprovalAsync(SqliteHrmsContext db, Seeded s, bool withWorkflow = true)
    {
        await using var seed = db.NewContext();
        Guid? workflowId = null;
        if (withWorkflow)
        {
            var workflow = new ApprovalWorkflow { Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, Name = "Leave" };
            workflow.Steps.Add(new ApprovalWorkflowStep
            {
                Id = Guid.NewGuid(), ApprovalWorkflowId = workflow.Id, StepNumber = 1,
                ApproverType = ApproverType.Person, ApproverEmployeeId = s.Caller,
            });
            seed.ApprovalWorkflows.Add(workflow);
            workflowId = workflow.Id;
        }
        var instance = new ApprovalInstance
        {
            Id = Guid.NewGuid(), ApplicationType = ApprovalApplicationType.Leave, ApplicationId = s.SecretLeaveApplication,
            ApplicantEmployeeId = s.Secret, ApprovalWorkflowId = workflowId, CurrentStepNumber = 1,
            Status = ApprovalInstanceStatus.InProgress,
        };
        seed.ApprovalInstances.Add(instance);
        await seed.SaveChangesAsync();
        return instance.Id;
    }

    [Fact]
    public async Task UnclearedCaller_DoesNotSeeTheConfidentialEmployee_OrAnythingAboutThem()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        var scope = await ScopeForCallerAsync(db);
        await using var context = db.NewContext(scope);

        scope.Restricted.Should().BeTrue();
        scope.HiddenEmployeeCount.Should().Be(1);
        (await context.Employees.Select(x => x.Id).ToListAsync()).Should().BeEquivalentTo([s.Public, s.Caller]);
        (await context.DailyTimeRecords.Select(x => x.EmployeeId).ToListAsync()).Should().BeEquivalentTo([s.Public]);
        (await context.LeaveCredits.AnyAsync()).Should().BeFalse();
        (await context.leaveApplications.AnyAsync()).Should().BeFalse();
        (await context.Dependents.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ClearedCaller_AndBackgroundWork_SeeEveryone()
    {
        using var db = new SqliteHrmsContext();
        await SeedAsync(db);

        // A cleared caller's scope is never initialized -- same as no scope at all.
        await using var cleared = db.NewContext(new ConfidentialAccessScope());
        await using var background = db.NewContext();

        (await cleared.Employees.CountAsync()).Should().Be(3);
        (await cleared.Dependents.CountAsync()).Should().Be(1);
        (await background.Employees.CountAsync()).Should().Be(3);
        (await background.DailyTimeRecords.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ConfidentialEmployee_AlwaysSeesTheirOwnRecords()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, secretIsCaller: true);
        var scope = await ScopeForCallerAsync(db);
        await using var context = db.NewContext(scope);

        scope.Restricted.Should().BeFalse("the only confidential employee is the caller themself");
        (await context.Employees.AnyAsync(x => x.Id == s.Secret)).Should().BeTrue();
        (await context.leaveApplications.AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task ConfidentialEmployee_NotLinkedYet_IsMatchedByEmail()
    {
        using var db = new SqliteHrmsContext();
        await SeedAsync(db);
        var scope = await ScopeForCallerAsync(db, email: "SAM@example.com ");

        scope.Restricted.Should().BeFalse();
    }

    [Fact]
    public async Task AssignedApprover_SeesJustThatRequest_NotTheEmployeesOtherRecords()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        var instanceId = await SeedApprovalAsync(db, s);
        var scope = await ScopeForCallerAsync(db);
        await using var context = db.NewContext(scope);

        (await context.leaveApplications.Select(x => x.Id).ToListAsync()).Should().BeEquivalentTo([s.SecretLeaveApplication]);
        (await context.ApprovalInstances.Select(x => x.Id).ToListAsync()).Should().BeEquivalentTo([instanceId]);
        (await context.Employees.AnyAsync(x => x.Id == s.Secret)).Should().BeTrue("the request shows who filed it");
        (await context.DailyTimeRecords.AnyAsync(x => x.EmployeeId == s.Secret)).Should().BeFalse();
        (await context.LeaveCredits.AnyAsync()).Should().BeFalse();
        scope.IsApprovalOnlyEmployee(s.Secret).Should().BeTrue();
    }

    [Fact]
    public async Task NoWorkflowFallbackStep_GrantsNoException()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        await SeedApprovalAsync(db, s, withWorkflow: false);
        var scope = await ScopeForCallerAsync(db);
        await using var context = db.NewContext(scope);

        (await context.leaveApplications.AnyAsync()).Should().BeFalse("fallback approvals go to cleared approvers");
        (await context.ApprovalInstances.AnyAsync()).Should().BeFalse();
    }

    // ── Clearance ──────────────────────────────────────────────────────────

    private static ClaimsPrincipal Principal(string? role = null, params string[] permissions)
    {
        var claims = new List<Claim>();
        if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void Clearance_Owner_IsAlwaysCleared() =>
        Principal("Owner").HasConfidentialClearance().Should().BeTrue();

    [Fact]
    public void Clearance_AdminWithoutTheGrant_IsNotCleared()
    {
        var admin = Principal("Admin");
        admin.HasPermission(ConfidentialAccessScope.ClearancePermission).Should().BeTrue("HasPermission's Admin shortcut");
        admin.HasConfidentialClearance().Should().BeFalse("the Owner decides which roles are cleared");
    }

    [Fact]
    public void Clearance_AnyRoleWithTheGrant_IsCleared() =>
        Principal("HR", ConfidentialAccessScope.ClearancePermission).HasConfidentialClearance().Should().BeTrue();

    // ── Employee service guards ────────────────────────────────────────────

    internal static EmployeeService EmployeeServiceFor(HrmsContext context, ConfidentialAccessScope? scope)
    {
        _ = TestMapping.GlobalSettings; // registers MappingProfile once per run (see TestMapping)
        var uow = new UnitOfWorkService(context);
        return new EmployeeService(
            uow, Substitute.For<IMapper>(), TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow), new PayrollGroupService(uow), new BranchService(uow),
            new CostCenterService(uow), new PositionService(uow), new SectionService(uow),
            access: scope);
    }

    [Fact]
    public async Task BioIdClash_WithAHiddenEmployee_IsStillRejected_WithoutNamingThem()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        var scope = await ScopeForCallerAsync(db);
        await using var context = db.NewContext(scope);
        var sut = EmployeeServiceFor(context, scope);

        var act = () => sut.AddAsync(new Employee { PayrollGroupId = s.PayrollGroupId, JobLevel = "", FirstName = "New", LastName = "Hire", BioId = 2 }, CancellationToken.None);

        (await act.Should().ThrowAsync<Exception>()).Which.Message.Should().Contain("Bio ID").And.NotContain("Secret");
    }

    [Fact]
    public async Task EmployeeScreens_LeaveOutAnApprovalOnlyApplicant()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        await SeedApprovalAsync(db, s);
        var scope = await ScopeForCallerAsync(db);
        await using var context = db.NewContext(scope);
        var sut = EmployeeServiceFor(context, scope);

        var list = await sut.SearchAsync(new EmployeeListQuery(), CancellationToken.None);

        list.Data.Select(x => x.Id).Should().BeEquivalentTo([s.Public, s.Caller]);
        (await sut.GetFullByIdAsync(s.Secret, CancellationToken.None)).Should().BeNull();
        sut.IsHiddenFromEmployeeScreens(s.Secret).Should().BeTrue();
        sut.IsHiddenFromEmployeeScreens(s.Public).Should().BeFalse();
    }

    // Regression: with a partial grant the employee is visible anyway, so approving one of their
    // requests must not take them off the employee screens (they used to vanish from e.g. Manual
    // Attendance Entry's employee search while others stayed).
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PartialGrant_Approver_StillSeesTheEmployee_OnEmployeeScreens(bool operations, bool entries)
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        await SeedApprovalAsync(db, s);
        var scope = await ScopeForCallerAsync(db, grants: new ConfidentialGrants(operations, entries));
        await using var context = db.NewContext(scope);
        var sut = EmployeeServiceFor(context, scope);

        (await sut.SearchAsync(new EmployeeListQuery(), CancellationToken.None)).Data.Select(x => x.Id)
            .Should().Contain(s.Secret);
        (await sut.Filter(new EmployeeFilter(), CancellationToken.None)).Select(x => x.Id)
            .Should().Contain(s.Secret);
        sut.IsHiddenFromEmployeeScreens(s.Secret).Should().BeFalse();
    }

    [Fact]
    public async Task PartialGrant_SeesTheConfidentialEmployee_InTheEmployeePicker()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        var scope = await ScopeForCallerAsync(db, grants: new ConfidentialGrants(Operations: true, Entries: false));
        await using var context = db.NewContext(scope);
        var sut = EmployeeServiceFor(context, scope);

        (await sut.Filter(new EmployeeFilter(), CancellationToken.None)).Select(x => x.Id)
            .Should().BeEquivalentTo([s.Public, s.Secret, s.Caller]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Update_ChangesTheTag_OnlyForACallerWithClearance(bool canChange, bool expected)
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        await using (var context = db.NewContext())
        {
            var sut = EmployeeServiceFor(context, null);
            var payload = new UpdateEmployee
            {
                Id = s.Public, PayrollGroupId = s.PayrollGroupId, FirstName = "Pat", LastName = "Public", BioId = 1, IsConfidential = true,
            };
            await sut.UpdateAsync(payload, CancellationToken.None, canChangeConfidentiality: canChange);
        }

        await using var check = db.NewContext();
        (await check.Employees.SingleAsync(x => x.Id == s.Public)).IsConfidential.Should().Be(expected);
    }

    [Fact]
    public async Task Update_ByAnInternalCaller_KeepsAnExistingTag()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        await using (var context = db.NewContext())
        {
            var sut = EmployeeServiceFor(context, null);
            var payload = new UpdateEmployee
            {
                Id = s.Secret, PayrollGroupId = s.PayrollGroupId, FirstName = "Sam", LastName = "Secret", BioId = 2, IsConfidential = false,
            };
            await sut.UpdateAsync(payload, CancellationToken.None);
        }

        await using var check = db.NewContext();
        (await check.Employees.SingleAsync(x => x.Id == s.Secret)).IsConfidential.Should().BeTrue();
    }

    [Fact]
    public async Task Update_OfAHiddenEmployee_IsNotFound()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        var scope = await ScopeForCallerAsync(db);
        await using var context = db.NewContext(scope);
        var sut = EmployeeServiceFor(context, scope);

        var act = () => sut.UpdateAsync(new UpdateEmployee { Id = s.Secret, PayrollGroupId = s.PayrollGroupId }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Import ─────────────────────────────────────────────────────────────

    private static MemoryStream Workbook(params (string BioId, string FirstName, string LastName)[] rows)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(1, 1).Value = "BioId";
        sheet.Cell(1, 2).Value = "FirstName";
        sheet.Cell(1, 3).Value = "LastName";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].BioId;
            sheet.Cell(i + 2, 2).Value = rows[i].FirstName;
            sheet.Cell(i + 2, 3).Value = rows[i].LastName;
        }
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task ImportPreview_FlagsRowsMatchingAHiddenEmployee_WithoutNamingThem()
    {
        using var db = new SqliteHrmsContext();
        await SeedAsync(db);
        var scope = await ScopeForCallerAsync(db);
        await using var context = db.NewContext(scope);
        var employeeService = EmployeeServiceFor(context, scope);
        var uow = employeeService.Uow;
        var sut = new EmployeeImportService(
            uow, employeeService,
            new TimeShiftService(uow, TypeAdapterConfig.GlobalSettings, Substitute.For<IMapper>()),
            new DepartmentService(uow), new PayrollGroupService(uow), new ClientService(uow),
            new BranchService(uow), new CostCenterService(uow));
        using var file = Workbook(("", "Sam", "Secret"), ("2", "Other", "Person"), ("", "Pat", "Public"));

        var rows = await sut.PreviewAsync(file, CancellationToken.None);

        rows[0].Errors.Should().BeEquivalentTo([EmployeeImportService.RestrictedMatchError], "same name as the hidden employee");
        rows[1].Errors.Should().BeEquivalentTo([EmployeeImportService.RestrictedMatchError], "same Bio ID as the hidden employee");
        rows[2].Errors.Should().BeEmpty();
        rows.SelectMany(r => r.Errors).Should().NotContain(e => e.Contains("Secret"));
    }

    // ── Assigned-asset cache ───────────────────────────────────────────────

    [Fact]
    public async Task AssetCache_IsNotServed_ForAHiddenEmployee()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        var scope = await ScopeForCallerAsync(db);
        await using var context = db.NewContext(scope);
        var cache = Substitute.For<Onepunch.Common.Lib.Cache.ICacheService>();
        cache.GetAsync<List<AssignAssetModel>>(Arg.Any<string>()).Returns([new AssignAssetModel { EmployeeId = s.Secret }]);
        var controller = new Hrms.Api.Controllers.EmployeeAssignAssetsController(
            new EmployeeAssignAssetService(new UnitOfWorkService(context)), new Mapper(), cache,
            Substitute.For<ITenantProvider>(), scope);

        var result = await controller.GetAll(s.Secret, CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<List<AssignAssetModel>>().Which.Should().BeEmpty();
        await cache.DidNotReceiveWithAnyArgs().GetAsync<List<AssignAssetModel>>(default!);
    }
}
