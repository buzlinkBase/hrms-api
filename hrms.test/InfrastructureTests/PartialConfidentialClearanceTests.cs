using System.Security.Claims;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Api.Middlewares;
using Hrms.Core.Services.Security;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using static hrms.test.InfrastructureTests.ConfidentialAccessTests;

namespace hrms.test.InfrastructureTests;

/// <summary>
/// Partial confidential clearances ("Confidential Records:Operations" / ":Entries"): a user may
/// process a confidential employee's day-to-day records or money entries without ever seeing their
/// pay -- payroll, contributions, pay rates, payslips and pay-based reports stay Access-only.
/// </summary>
public class PartialConfidentialClearanceTests
{
    private static readonly ConfidentialGrants OperationsOnly = new(Operations: true, Entries: false);
    private static readonly ConfidentialGrants EntriesOnly = new(Operations: false, Entries: true);

    // The seeded confidential employee also gets pay, a payroll run (with a cash bond deduction),
    // a manual cash bond entry and a retirement ledger row.
    private static async Task<Seeded> SeedWithPayAsync(SqliteHrmsContext db)
    {
        var s = await SeedAsync(db);
        await using var seed = db.NewContext();
        var secret = await seed.Employees.SingleAsync(x => x.Id == s.Secret);
        secret.MonthlyRate = 90_000m;
        secret.DailyRate = 4_000m;
        secret.BankNo = "1234-5678";
        seed.Payrolls.Add(new Payroll
        {
            Id = Guid.NewGuid(), EmployeeId = s.Secret, CashBondDeduction = 100m,
            PayPeriodStart = new DateOnly(2026, 1, 1), PayPeriodEnd = new DateOnly(2026, 1, 15),
        });
        seed.CashBondAdjustments.Add(new CashBondAdjustment
        {
            Id = Guid.NewGuid(), EmployeeId = s.Secret, EntryDate = new DateOnly(2026, 1, 2), Amount = 500m, Particulars = "Opening",
        });
        var fund = new RetirementFund { Id = Guid.NewGuid(), EmployeeId = s.Secret, Balance = 50m };
        seed.Set<RetirementFund>().Add(fund);
        seed.RetirementLedgers.Add(new RetirementLedger
        {
            Id = Guid.NewGuid(), EmployeeId = s.Secret, RetirementFundId = fund.Id,
            EntryDate = new DateOnly(2026, 1, 15), Amount = 50m, Balance = 50m,
        });
        await seed.SaveChangesAsync();
        return s;
    }

    [Fact]
    public async Task OperationsOnly_SeesDayToDayRecords_ButNoMoneyEntriesOrPay()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedWithPayAsync(db);
        var scope = await ScopeForCallerAsync(db, grants: OperationsOnly);
        await using var context = db.NewContext(scope);

        (await context.Employees.AnyAsync(x => x.Id == s.Secret)).Should().BeTrue();
        (await context.DailyTimeRecords.AnyAsync(x => x.EmployeeId == s.Secret)).Should().BeTrue();
        (await context.leaveApplications.AnyAsync()).Should().BeTrue();
        (await context.LeaveCredits.AnyAsync()).Should().BeTrue();
        (await context.Dependents.AnyAsync()).Should().BeTrue();

        (await context.CashBondAdjustments.AnyAsync()).Should().BeFalse("cash bond is an Entries record");
        (await context.Payrolls.AnyAsync()).Should().BeFalse("payroll is Access-only");
        (await context.RetirementLedgers.AnyAsync()).Should().BeFalse("retirement follows pay");
        scope.HiddenEmployeeCount.Should().Be(1, "reports still leave them out");
    }

    [Fact]
    public async Task EntriesOnly_SeesMoneyEntries_ButNotDayToDayRecordsOrPay()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedWithPayAsync(db);
        var scope = await ScopeForCallerAsync(db, grants: EntriesOnly);
        await using var context = db.NewContext(scope);

        (await context.Employees.AnyAsync(x => x.Id == s.Secret)).Should().BeTrue("they must be able to pick the employee");
        (await context.CashBondAdjustments.AnyAsync()).Should().BeTrue();

        (await context.DailyTimeRecords.AnyAsync(x => x.EmployeeId == s.Secret)).Should().BeFalse();
        (await context.leaveApplications.AnyAsync()).Should().BeFalse();
        (await context.Payrolls.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task EntriesOnly_CashBondLedger_IncludesTheDeductionFromPayroll()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedWithPayAsync(db);
        var scope = await ScopeForCallerAsync(db, grants: EntriesOnly);
        await using var context = db.NewContext(scope);
        var employees = EmployeeServiceFor(context, scope);
        var reports = new PayrollReportService(employees.Uow, employees, null!, scope);

        var entries = await reports.GetCashBondEntriesAsync(s.Secret, new DateOnly(2026, 12, 31), CancellationToken.None);
        var summary = await reports.GetCashBondReportAsync(new DateOnly(2026, 12, 31), CancellationToken.None);

        entries.Select(e => e.Amount).Should().BeEquivalentTo([500m, 100m]);
        summary.Single(x => x.EmployeeId == s.Secret).TotalCollected.Should().Be(600m);
    }

    [Fact]
    public async Task OperationsOnly_CashBondLedger_LeavesTheConfidentialEmployeeOut()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedWithPayAsync(db);
        var scope = await ScopeForCallerAsync(db, grants: OperationsOnly);
        await using var context = db.NewContext(scope);
        var employees = EmployeeServiceFor(context, scope);
        var reports = new PayrollReportService(employees.Uow, employees, null!, scope);

        (await reports.GetCashBondReportAsync(new DateOnly(2026, 12, 31), CancellationToken.None))
            .Should().NotContain(x => x.EmployeeId == s.Secret);
    }

    [Fact]
    public async Task PartialClearance_GetsThePayFieldsBlanked_OthersDoNot()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedWithPayAsync(db);
        var scope = await ScopeForCallerAsync(db, grants: OperationsOnly);
        await using var context = db.NewContext(scope);
        var sut = EmployeeServiceFor(context, scope);

        var secret = await sut.GetFullByIdAsync(s.Secret, CancellationToken.None);
        secret!.MonthlyRate.Should().Be(0);
        secret.DailyRate.Should().Be(0);
        secret.BankNo.Should().BeEmpty();
        secret.FiguresHidden.Should().BeTrue();

        var list = await sut.SearchAsync(new EmployeeListQuery(), CancellationToken.None);
        list.Data!.Single(x => x.Id == s.Secret).FiguresHidden.Should().BeTrue();
        list.Data!.Single(x => x.Id == s.Public).FiguresHidden.Should().BeFalse();
    }

    [Fact]
    public async Task PartialClearance_SavingTheEmployee_KeepsTheStoredPay()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedWithPayAsync(db);
        var scope = await ScopeForCallerAsync(db, grants: OperationsOnly);
        await using (var context = db.NewContext(scope))
        {
            // What the masked form sends back: blank pay, a changed name.
            await EmployeeServiceFor(context, scope).UpdateAsync(new UpdateEmployee
            {
                Id = s.Secret, PayrollGroupId = s.PayrollGroupId, FirstName = "Samuel", LastName = "Secret", BioId = 2,
                MonthlyRate = 0, DailyRate = 0, BankNo = "",
            }, CancellationToken.None);
        }

        await using var check = db.NewContext();
        var stored = await check.Employees.SingleAsync(x => x.Id == s.Secret);
        stored.FirstName.Should().Be("Samuel");
        stored.MonthlyRate.Should().Be(90_000m);
        stored.DailyRate.Should().Be(4_000m);
        stored.BankNo.Should().Be("1234-5678");
    }

    // ── Grants and the pay-figure endpoint rule ────────────────────────────

    private static readonly Guid Tenant = Guid.NewGuid();

    private static ClaimsPrincipal SignedIn(Guid userId, params string[] permissions)
    {
        var claims = new List<Claim> { new("sub", userId.ToString()), new(ClaimTypes.Role, "HR") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void Grants_AreReadFromExplicitPermissionsOnly()
    {
        var both = SignedIn(Guid.NewGuid(), ConfidentialAccessScope.OperationsPermission, ConfidentialAccessScope.EntriesPermission);
        both.GetConfidentialGrants().Should().Be(new ConfidentialGrants(true, true));

        var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "test"));
        admin.GetConfidentialGrants().Should().Be(ConfidentialGrants.None, "Admin gets no shortcut");
    }

    private static async Task<ConfidentialAccessScope> RunMiddlewareAsync(
        SqliteHrmsContext db, string[] permissions, params object[] endpointMetadata)
    {
        var http = new DefaultHttpContext { User = SignedIn(Guid.NewGuid(), permissions) };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(endpointMetadata), "test"));
        var tenant = Substitute.For<ITenantProvider>();
        tenant.TenantId.Returns(Tenant);

        var scope = new ConfidentialAccessScope();
        await using var context = db.NewContext();
        await new ConfidentialAccessMiddleware(_ => Task.CompletedTask).InvokeAsync(http, scope, context, tenant);
        return scope;
    }

    private static async Task<Guid> SeedTenantConfidentialAsync(SqliteHrmsContext db)
    {
        var secretId = Guid.NewGuid();
        await using var seed = db.NewContext();
        var group = new PayrollGroup { Id = Guid.NewGuid(), TenantId = Tenant, Name = "Main" };
        seed.PayrollGroups.Add(group);
        seed.Employees.Add(new Employee { Id = secretId, TenantId = Tenant, PayrollGroupId = group.Id, IsConfidential = true });
        await seed.SaveChangesAsync();
        return secretId;
    }

    private static readonly string[] Operations = [ConfidentialAccessScope.OperationsPermission];
    private static readonly string[] Entries = [ConfidentialAccessScope.EntriesPermission];
    private static readonly string[] Both = [ConfidentialAccessScope.OperationsPermission, ConfidentialAccessScope.EntriesPermission];

    [Fact]
    public async Task UntaggedEndpoint_KeepsBothGrants()
    {
        using var db = new SqliteHrmsContext();
        var secretId = await SeedTenantConfidentialAsync(db);

        var scope = await RunMiddlewareAsync(db, Both);

        scope.CanSeeEmployee(secretId).Should().BeTrue();
        scope.CanSee(ConfidentialDataClass.Operations, secretId).Should().BeTrue();
        scope.CanSee(ConfidentialDataClass.Entries, secretId).Should().BeTrue();
        scope.CanSee(ConfidentialDataClass.Compensation, secretId).Should().BeFalse();
    }

    [Fact]
    public async Task EntriesOnly_OnAnOperationsEndpoint_DoesNotGetTheEmployee()
    {
        using var db = new SqliteHrmsContext();
        var secretId = await SeedTenantConfidentialAsync(db);

        var scope = await RunMiddlewareAsync(db, Entries, new OperationsDataAttribute());

        scope.CanSeeEmployee(secretId).Should().BeFalse("a DTR run must not list them and generate absences");
        scope.CanSee(ConfidentialDataClass.Operations, secretId).Should().BeFalse();
    }

    [Fact]
    public async Task OperationsOnly_OnAnEntriesEndpoint_DoesNotGetTheEmployee()
    {
        using var db = new SqliteHrmsContext();
        var secretId = await SeedTenantConfidentialAsync(db);

        var scope = await RunMiddlewareAsync(db, Operations, new EntriesDataAttribute());

        scope.CanSeeEmployee(secretId).Should().BeFalse();
        scope.CanSee(ConfidentialDataClass.Entries, secretId).Should().BeFalse();
    }

    [Fact]
    public async Task MatchingGrant_OnItsEndpoint_GetsTheEmployee()
    {
        using var db = new SqliteHrmsContext();
        var secretId = await SeedTenantConfidentialAsync(db);

        (await RunMiddlewareAsync(db, Operations, new OperationsDataAttribute())).CanSeeEmployee(secretId).Should().BeTrue();
        (await RunMiddlewareAsync(db, Entries, new EntriesDataAttribute())).CanSeeEmployee(secretId).Should().BeTrue();
    }

    [Fact]
    public async Task CompensationEndpoint_HidesThemFromEveryPartialGrant()
    {
        using var db = new SqliteHrmsContext();
        var secretId = await SeedTenantConfidentialAsync(db);

        var scope = await RunMiddlewareAsync(db, Both, new CompensationDataAttribute());

        scope.Restricted.Should().BeTrue();
        scope.CanSeeEmployee(secretId).Should().BeFalse("payroll generation needs full Access");
        scope.CanSee(ConfidentialDataClass.Operations, secretId).Should().BeFalse();
        scope.CanSee(ConfidentialDataClass.Entries, secretId).Should().BeFalse();
    }

    [Fact]
    public async Task ActionLevelAttribute_OverridesTheControllers()
    {
        using var db = new SqliteHrmsContext();
        var secretId = await SeedTenantConfidentialAsync(db);

        // Endpoint metadata lists controller attributes first, then the action's.
        var scope = await RunMiddlewareAsync(db, Entries, new CompensationDataAttribute(), new EntriesDataAttribute());

        scope.CanSeeEmployee(secretId).Should().BeTrue();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task PayFigureEndpoints_IgnoreThePartialClearance(bool compensationEndpoint, bool expectedToSeeDtr)
    {
        using var db = new SqliteHrmsContext();
        var secretId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            var group = new PayrollGroup { Id = Guid.NewGuid(), TenantId = Tenant, Name = "Main" };
            seed.PayrollGroups.Add(group);
            seed.Employees.Add(new Employee { Id = secretId, TenantId = Tenant, PayrollGroupId = group.Id, IsConfidential = true });
            await seed.SaveChangesAsync();
        }

        var scope = await RunMiddlewareAsync(db, Operations, compensationEndpoint ? [new CompensationDataAttribute()] : []);

        scope.Restricted.Should().BeTrue();
        scope.CanSee(ConfidentialDataClass.Operations, secretId).Should().Be(expectedToSeeDtr);
        scope.CanSee(ConfidentialDataClass.Compensation, secretId).Should().BeFalse();
    }


    // ── Operations that iterate employees follow the narrowed scope ────────

    [Fact]
    public async Task DtrRun_ByAnEntriesOnlyUser_LeavesTheConfidentialEmployeeOut()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        var dtrPayload = new DTRRequestPayload { FromDate = new DateOnly(2026, 3, 1), ToDate = new DateOnly(2026, 3, 15) };

        var entriesOnDtr = await ScopeForCallerAsync(db, grants: EntriesOnly.NarrowTo(ConfidentialDataClass.Operations));
        await using (var context = db.NewContext(entriesOnDtr))
        {
            (await EmployeeServiceFor(context, entriesOnDtr).GetForDTRRunAsync(dtrPayload, CancellationToken.None))
                .Select(x => x.Id).Should().NotContain(s.Secret);
        }

        var operationsOnDtr = await ScopeForCallerAsync(db, grants: OperationsOnly.NarrowTo(ConfidentialDataClass.Operations));
        await using (var context = db.NewContext(operationsOnDtr))
        {
            (await EmployeeServiceFor(context, operationsOnDtr).GetForDTRRunAsync(dtrPayload, CancellationToken.None))
                .Select(x => x.Id).Should().Contain(s.Secret);
        }
    }

    [Fact]
    public async Task PayrollGeneration_WithoutFullAccess_LeavesTheConfidentialEmployeeOut()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db);
        await using (var seed = db.NewContext())
        {
            seed.Set<EmployeeSetting>().Add(new EmployeeSetting { Id = Guid.NewGuid(), EmployeeId = s.Secret, IsEligibleFor13thMonth = true });
            var secret = await seed.Employees.SingleAsync(x => x.Id == s.Secret);
            secret.EmploymentStatus = EmploymentStatus.Resigned;
            secret.DateResigned = new DateTime(2026, 3, 31);
            await seed.SaveChangesAsync();
        }

        // What a payroll endpoint ([CompensationData]) leaves of Operations + Entries.
        var grants = new ConfidentialGrants(true, true).NarrowTo(ConfidentialDataClass.Compensation);
        var scope = await ScopeForCallerAsync(db, grants: grants);
        await using var context = db.NewContext(scope);
        _ = TestMapping.GlobalSettings;
        var uow = new UnitOfWorkService(context);
        var sut = new EmployeeService(
            uow, new Mapper(TypeAdapterConfig.GlobalSettings), TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow), new PayrollGroupService(uow), new BranchService(uow),
            new CostCenterService(uow), new PositionService(uow), new SectionService(uow), access: scope);

        (await sut.GetForThirteenthMonthRunAsync(null, null, CancellationToken.None)).Select(x => x.Id).Should().NotContain(s.Secret);
        (await sut.GetSeparatedEmployeesForLastPayAsync(null, CancellationToken.None)).Select(x => x.Id).Should().NotContain(s.Secret);
    }

    [Fact]
    public async Task AttendanceUpload_StillAttachesAConfidentialEmployeesPunches()
    {
        using var db = new SqliteHrmsContext();
        await SeedAsync(db); // the confidential employee has Bio ID 2
        var scope = await ScopeForCallerAsync(db, grants: ConfidentialGrants.None);
        await using var context = db.NewContext(scope);

        var atts = await new OnePunch.Auth.Core.Messaging.AttEmployeeSetter(new UnitOfWorkService(context)).ParseAttLogs(
            [new Onepunch.Common.Lib.DTO.CreateAttendancePayload { BatchId = "b1", BioId = 2, WorkDateTime = new DateTime(2026, 3, 2, 8, 0, 0) }],
            LOGSOURCE.UPLOADED);

        atts.Single().EmployeeId.Should().NotBeNull("device punches never become unregistered because of who uploaded them");
    }

    // ── Every operation keeps its declared class ───────────────────────────

    [Theory]
    [InlineData(typeof(Hrms.Api.Controllers.DailyRecordsController), ConfidentialDataClass.Operations)]
    [InlineData(typeof(Hrms.Api.Controllers.AttendanceController), ConfidentialDataClass.Operations)]
    [InlineData(typeof(Hrms.Api.Controllers.WorkSchedulePlansController), ConfidentialDataClass.Operations)]
    [InlineData(typeof(Hrms.Api.Controllers.LeaveApplicationsController), ConfidentialDataClass.Operations)]
    [InlineData(typeof(Hrms.Api.Controllers.OvertimeApplicationsController), ConfidentialDataClass.Operations)]
    [InlineData(typeof(Hrms.Api.Controllers.CashBondController), ConfidentialDataClass.Entries)]
    [InlineData(typeof(Hrms.Api.Controllers.OtherIncomeApplicationApplicationsController), ConfidentialDataClass.Entries)]
    [InlineData(typeof(Hrms.Api.Controllers.DeductionApplicationsApplicationsController), ConfidentialDataClass.Entries)]
    [InlineData(typeof(Hrms.Api.Controllers.UniformAllowanceController), ConfidentialDataClass.Entries)]
    [InlineData(typeof(Hrms.Api.Controllers.PayrollsController), ConfidentialDataClass.Compensation)]
    [InlineData(typeof(Hrms.Api.Controllers.SalaryAdjustmentmentController), ConfidentialDataClass.Compensation)]
    public void Controllers_DeclareTheirConfidentialDataClass(Type controller, ConfidentialDataClass expected)
    {
        var attribute = (ConfidentialDataAttribute?)Attribute.GetCustomAttribute(controller, typeof(ConfidentialDataAttribute));
        attribute.Should().NotBeNull($"{controller.Name} must declare the class of data it works on");
        attribute!.DataClass.Should().Be(expected);
    }
}
