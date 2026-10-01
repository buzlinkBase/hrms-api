using System.Reflection;
using System.Security.Claims;
using Hrms.Api.Controllers;
using Hrms.Api.Filters;
using Hrms.Api.Middlewares;
using Hrms.Core.Services.Subscriptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Onepunch.Common.Lib.Subscriptions;

namespace hrms.test.SubscriptionTests;

internal static class Snapshots
{
    public static readonly Guid Company = Guid.NewGuid();

    public static EntitlementSnapshot With(
        SubscriptionAccessMode access = SubscriptionAccessMode.Full,
        bool payroll = true,
        int employeesMax = EntitlementKeys.Unlimited) => new()
    {
        TenantId = Company,
        Status = access == SubscriptionAccessMode.Full ? "Active" : "Expired",
        AccessMode = access,
        Features = EntitlementKeys.Features.ToDictionary(k => k, k => k != EntitlementKeys.PayrollCore || payroll),
        Limits = EntitlementKeys.Limits.ToDictionary(k => k, k => k == EntitlementKeys.EmployeesMax ? employeesMax : EntitlementKeys.Unlimited),
    };

    public static IEntitlementProvider Provider(EntitlementSnapshot snapshot)
    {
        var provider = Substitute.For<IEntitlementProvider>();
        provider.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(snapshot);
        return provider;
    }

    public static ITenantProvider Tenant()
    {
        var tenant = Substitute.For<ITenantProvider>();
        tenant.TenantId.Returns(Company);
        return tenant;
    }
}

/// <summary>[RequireFeature]: the plan, not the person, decides -- and reads can be left open.</summary>
public class RequireFeatureAttributeTests
{
    private static ActionExecutingContext BuildContext(EntitlementSnapshot snapshot, string method = "POST")
    {
        var services = new ServiceCollection()
            .AddSingleton(Snapshots.Provider(snapshot))
            .AddSingleton(Snapshots.Tenant())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Method = method;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
    }

    private static ActionExecutionDelegate Next(ActionExecutingContext context, Action onCalled) => () =>
    {
        onCalled();
        return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
    };

    [Fact]
    public async Task Allows_WhenThePlanIncludesTheFeature()
    {
        var context = BuildContext(Snapshots.With(payroll: true));
        var called = false;

        await new RequireFeatureAttribute(EntitlementKeys.PayrollCore).OnActionExecutionAsync(context, Next(context, () => called = true));

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Refuses_WithPlanRequired_WhenThePlanLacksIt()
    {
        var context = BuildContext(Snapshots.With(payroll: false));

        var act = () => new RequireFeatureAttribute(EntitlementKeys.PayrollCore).OnActionExecutionAsync(context, Next(context, () => { }));

        var error = await act.Should().ThrowAsync<PlanRestrictionException>();
        error.Which.ProblemType.Should().Be(SubscriptionProblemTypes.PlanRequired);
        error.Which.Extensions["feature"].Should().Be(EntitlementKeys.PayrollCore);
    }

    [Fact]
    public async Task WritesOnly_LetsReadsThrough()
    {
        var context = BuildContext(Snapshots.With(payroll: false), method: "GET");
        var called = false;

        await new RequireFeatureAttribute(EntitlementKeys.PayrollCore) { WritesOnly = true }
            .OnActionExecutionAsync(context, Next(context, () => called = true));

        called.Should().BeTrue();
    }

    [Fact]
    public async Task WritesOnly_StillRefusesWrites()
    {
        var context = BuildContext(Snapshots.With(payroll: false), method: "POST");

        var act = () => new RequireFeatureAttribute(EntitlementKeys.PayrollCore) { WritesOnly = true }
            .OnActionExecutionAsync(context, Next(context, () => { }));

        await act.Should().ThrowAsync<PlanRestrictionException>();
    }
}

/// <summary>
/// A lapsed subscription: the owner and billing admins can look but not change; every other
/// member is locked out entirely. Enforced on the server.
/// </summary>
public class SubscriptionAccessMiddlewareTests
{
    private static Claim Role(string role) => new(System.Security.Claims.ClaimTypes.Role, role);
    private static Claim Permission(string code) => new("permission", code);

    private static async Task<bool> RunAsync(
        string method,
        SubscriptionAccessMode access,
        Claim[]? claims = null,
        bool authenticated = true,
        bool allowWhenReadOnly = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/api/v1/employees";
        if (authenticated)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", Guid.NewGuid().ToString()), .. claims ?? [Role("Owner")]], "Bearer"));
        }
        if (allowWhenReadOnly)
            context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowWhenReadOnlyAttribute()), "export"));

        var reached = false;
        var middleware = new SubscriptionAccessMiddleware(_ => { reached = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(context, Snapshots.Tenant(), Snapshots.Provider(Snapshots.With(access)));
        return reached;
    }

    // ── Owner / billing admin: read-only ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Owner_ReadOnly_RefusesWrites(string method)
    {
        var act = () => RunAsync(method, SubscriptionAccessMode.ReadOnly);

        (await act.Should().ThrowAsync<PlanRestrictionException>()).Which.ProblemType.Should().Be(SubscriptionProblemTypes.ReadOnly);
    }

    [Fact]
    public async Task Owner_ReadOnly_StillAllowsReads() =>
        (await RunAsync("GET", SubscriptionAccessMode.ReadOnly)).Should().BeTrue();

    [Fact]
    public async Task Owner_ReadOnly_AllowsEndpointsMarkedAsReadOnlySafe() =>
        (await RunAsync("POST", SubscriptionAccessMode.ReadOnly, allowWhenReadOnly: true)).Should().BeTrue();

    [Fact]
    public async Task BillingAdmin_GetsTheOwnersTreatment() =>
        (await RunAsync("GET", SubscriptionAccessMode.ReadOnly, [Role("Member"), Permission("Billing:View")])).Should().BeTrue();

    // ── Everyone else: locked out ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GET", "Employee")]
    [InlineData("GET", "Member")]
    [InlineData("GET", "Admin")]
    [InlineData("POST", "Admin")]
    public async Task Member_ReadOnly_IsLockedOut_ReadsIncluded(string method, string role)
    {
        var act = () => RunAsync(method, SubscriptionAccessMode.ReadOnly, [Role(role), Permission("Payroll Run:View")]);

        (await act.Should().ThrowAsync<PlanRestrictionException>()).Which.ProblemType.Should().Be(SubscriptionProblemTypes.Locked);
    }

    [Fact]
    public async Task Member_ReadOnlySafePost_IsStillLocked()
    {
        var act = () => RunAsync("POST", SubscriptionAccessMode.ReadOnly, [Role("Member")], allowWhenReadOnly: true);

        (await act.Should().ThrowAsync<PlanRestrictionException>()).Which.ProblemType.Should().Be(SubscriptionProblemTypes.Locked);
    }

    // ── Unaffected ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnonymousCalls_AreNotCompanyUsers_AndPass() =>
        (await RunAsync("POST", SubscriptionAccessMode.ReadOnly, authenticated: false)).Should().BeTrue();

    [Theory]
    [InlineData("Owner")]
    [InlineData("Employee")]
    public async Task FullAccess_AllowsEveryone(string role) =>
        (await RunAsync("POST", SubscriptionAccessMode.Full, [Role(role)])).Should().BeTrue();
}
public class PlanLimitGuardTests
{
    private static PlanLimitGuard Guard(EntitlementSnapshot snapshot, int used)
    {
        var counter = Substitute.For<ILimitUsageCounter>();
        counter.LimitKey.Returns(EntitlementKeys.EmployeesMax);
        counter.CountAsync(Arg.Any<CancellationToken>()).Returns(used);
        return new PlanLimitGuard(Snapshots.Provider(snapshot), Snapshots.Tenant(), [counter]);
    }

    [Fact]
    public async Task WithinTheLimit_Passes()
    {
        var act = () => Guard(Snapshots.With(employeesMax: 100), used: 99).EnsureCanAddAsync(EntitlementKeys.EmployeesMax, 1, default);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PastTheLimit_IsRefused_WithTheCounts()
    {
        var act = () => Guard(Snapshots.With(employeesMax: 100), used: 100).EnsureCanAddAsync(EntitlementKeys.EmployeesMax, 1, default);

        var error = await act.Should().ThrowAsync<PlanRestrictionException>();
        error.Which.ProblemType.Should().Be(SubscriptionProblemTypes.PlanLimitReached);
        error.Which.Extensions["max"].Should().Be(100);
        error.Which.Extensions["used"].Should().Be(100);
    }

    [Fact]
    public async Task ABatch_MustFitWhole()
    {
        var act = () => Guard(Snapshots.With(employeesMax: 100), used: 95).EnsureCanAddAsync(EntitlementKeys.EmployeesMax, 10, default);
        await act.Should().ThrowAsync<PlanRestrictionException>();
    }

    [Fact]
    public async Task Unlimited_NeverCounts()
    {
        var counter = Substitute.For<ILimitUsageCounter>();
        counter.LimitKey.Returns(EntitlementKeys.EmployeesMax);
        var guard = new PlanLimitGuard(Snapshots.Provider(Snapshots.With()), Snapshots.Tenant(), [counter]);

        await guard.EnsureCanAddAsync(EntitlementKeys.EmployeesMax, 1000, default);

        await counter.DidNotReceive().CountAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadOnly_RefusesAdding()
    {
        var act = () => Guard(Snapshots.With(SubscriptionAccessMode.ReadOnly, employeesMax: 100), used: 1).EnsureCanAddAsync(EntitlementKeys.EmployeesMax, 1, default);
        (await act.Should().ThrowAsync<PlanRestrictionException>()).Which.ProblemType.Should().Be(SubscriptionProblemTypes.ReadOnly);
    }
}

/// <summary>
/// Every action in a paid module carries [RequireFeature] (on itself or its controller), so a
/// new endpoint can't ship ungated by accident. Mirrors the coverage table in the plan and the
/// frontend's navigation feature map.
/// </summary>
public class SubscriptionFeatureConventionTests
{
    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(Task<IActionResult>) || m.ReturnType == typeof(IActionResult)
                || (m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)));

    private static string[] FeaturesOn(Type controller, MethodInfo action) =>
        controller.GetCustomAttributes<RequireFeatureAttribute>()
            .Concat(action.GetCustomAttributes<RequireFeatureAttribute>())
            .Select(a => a.Feature)
            .ToArray();

    public static IEnumerable<object[]> GatedControllers() =>
    [
        [typeof(AnalyticsController), EntitlementKeys.Analytics],
        [typeof(AnnouncementsController), EntitlementKeys.Announcements],
        [typeof(AuditLogsController), EntitlementKeys.AuditTrail],
        [typeof(PayrollsController), EntitlementKeys.PayrollCore],
        [typeof(CashBondController), EntitlementKeys.PayrollLoansIncome],
        [typeof(DeductionApplicationsApplicationsController), EntitlementKeys.PayrollLoansIncome],
        [typeof(OtherIncomeApplicationApplicationsController), EntitlementKeys.PayrollLoansIncome],
        [typeof(SalaryAdjustmentmentController), EntitlementKeys.PayrollLoansIncome],
        [typeof(RetirementController), EntitlementKeys.PayrollLoansIncome],
        [typeof(UniformAllowanceController), EntitlementKeys.PayrollLoansIncome],
        [typeof(LeaveApplicationsController), EntitlementKeys.ApplicationsCore],
        [typeof(OvertimeApplicationsController), EntitlementKeys.ApplicationsCore],
        [typeof(PassSlipApplicationsController), EntitlementKeys.ApplicationsCore],
        [typeof(TravelOrderApplicationsController), EntitlementKeys.ApplicationsCore],
        [typeof(UnderTimeApplicationsController), EntitlementKeys.ApplicationsCore],
        [typeof(AttendanceController), EntitlementKeys.TimekeepingCore],
        [typeof(DailyRecordsController), EntitlementKeys.TimekeepingCore],
        [typeof(WorkSchedulePlansController), EntitlementKeys.TimekeepingCore],
        [typeof(ChangeRestDaysController), EntitlementKeys.TimekeepingCore],
        [typeof(ChangeHolidaysController), EntitlementKeys.TimekeepingCore],
        [typeof(MeController), EntitlementKeys.PortalSelfService],
    ];

    [Theory]
    [MemberData(nameof(GatedControllers))]
    public void EveryAction_InAPaidModule_RequiresItsFeature(Type controller, string feature)
    {
        foreach (var action in Actions(controller))
        {
            FeaturesOn(controller, action).Should().Contain(feature, $"{controller.Name}.{action.Name} belongs to {feature}");
        }
    }

    [Fact]
    public void EveryPayrollReport_RequiresAFeature()
    {
        var ungated = Actions(typeof(PayrollReportsController))
            .Where(a => FeaturesOn(typeof(PayrollReportsController), a).Length == 0)
            .Select(a => a.Name)
            .ToList();

        ungated.Should().BeEmpty();
    }

    [Theory]
    [InlineData(nameof(PayrollReportsController.SssRemittance), EntitlementKeys.ReportsStatutory)]
    [InlineData(nameof(PayrollReportsController.Bir2316Print), EntitlementKeys.ReportsStatutory)]
    [InlineData(nameof(PayrollReportsController.BankDisbursement), EntitlementKeys.PayrollCore)]
    [InlineData(nameof(PayrollReportsController.DeductionLedger), EntitlementKeys.PayrollLoansIncome)]
    [InlineData(nameof(PayrollReportsController.LeaveLedger), EntitlementKeys.ApplicationsCore)]
    [InlineData(nameof(PayrollReportsController.CostSummary), EntitlementKeys.ClientsAgency)]
    public void PayrollReport_RequiresTheRightFeature(string action, string feature)
    {
        var method = typeof(PayrollReportsController).GetMethod(action)!;
        FeaturesOn(typeof(PayrollReportsController), method).Should().ContainSingle().Which.Should().Be(feature);
    }

    [Theory]
    [InlineData(nameof(MeController.GetMyPayrolls), EntitlementKeys.PayrollCore)]
    [InlineData(nameof(MeController.CreateMyLeaveApplication), EntitlementKeys.ApplicationsCore)]
    [InlineData(nameof(MeController.CreateMyLoanApplication), EntitlementKeys.PayrollLoansIncome)]
    public void PortalAction_AlsoRequiresItsModulesFeature(string action, string feature)
    {
        var method = typeof(MeController).GetMethod(action)!;
        FeaturesOn(typeof(MeController), method).Should().BeEquivalentTo([EntitlementKeys.PortalSelfService, feature]);
    }

    [Fact]
    public void ReferenceDataSetups_AreGatedOnWritesOnly()
    {
        foreach (var controller in new[] { typeof(ClientsController), typeof(DeductionsController), typeof(SSSController), typeof(MinimumWageRatesController) })
        {
            controller.GetCustomAttribute<RequireFeatureAttribute>()!.WritesOnly
                .Should().BeTrue($"{controller.Name}'s lists are read by other modules' forms");
        }
    }

    [Theory]
    [InlineData(nameof(DailyRecordsController.Summary))]
    [InlineData(nameof(DailyRecordsController.Details))]
    public void ReadOnlyPosts_AreAllowedWhileReadOnly(string action) =>
        typeof(DailyRecordsController).GetMethod(action)!.GetCustomAttribute<AllowWhenReadOnlyAttribute>().Should().NotBeNull();
}
