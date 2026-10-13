using Hrms.Api.Controllers;
using Hrms.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Onepunch.Common.Lib.Subscriptions;

namespace hrms.test.SubscriptionTests;

/// <summary>The HRMS system switched off in the license refuses every HRMS endpoint, Owner included.</summary>
public class HrmsProductGateFilterTests
{
    private static ActionExecutingContext BuildContext(bool hrms, params object[] endpointMetadata)
    {
        var snapshot = Snapshots.With();
        snapshot.Features[EntitlementKeys.HrmsCore] = hrms;
        var services = new ServiceCollection()
            .AddSingleton(Snapshots.Provider(snapshot))
            .AddSingleton(Snapshots.Tenant())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Method = "GET";
        var descriptor = new ActionDescriptor { EndpointMetadata = endpointMetadata.ToList() };
        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
    }

    private static ActionExecutionDelegate Next(ActionExecutingContext context, Action onCalled) => () =>
    {
        onCalled();
        return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
    };

    [Fact]
    public async Task Allows_WhenThePlanIncludesHrms()
    {
        var context = BuildContext(hrms: true);
        var called = false;

        await new HrmsProductGateFilter().OnActionExecutionAsync(context, Next(context, () => called = true));

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Refuses_WithPlanRequired_WhenHrmsIsSwitchedOff()
    {
        var context = BuildContext(hrms: false);

        var act = () => new HrmsProductGateFilter().OnActionExecutionAsync(context, Next(context, () => { }));

        var error = await act.Should().ThrowAsync<PlanRestrictionException>();
        error.Which.ProblemType.Should().Be(SubscriptionProblemTypes.PlanRequired);
        error.Which.Extensions["feature"].Should().Be(EntitlementKeys.HrmsCore);
    }

    [Fact]
    public async Task LetsAnonymousEndpointsThrough_WhenHrmsIsSwitchedOff()
    {
        var context = BuildContext(false, new AllowAnonymousAttribute());
        var called = false;

        await new HrmsProductGateFilter().OnActionExecutionAsync(context, Next(context, () => called = true));

        called.Should().BeTrue();
    }

    [Fact]
    public async Task LetsMarkedEndpointsThrough_WhenHrmsIsSwitchedOff()
    {
        var context = BuildContext(false, new AllowWithoutHrmsProductAttribute());
        var called = false;

        await new HrmsProductGateFilter().OnActionExecutionAsync(context, Next(context, () => called = true));

        called.Should().BeTrue();
    }

    [Fact]
    public void TenantSeeder_StaysAvailable_BecauseProvisioningRunsBeforeThePlanHasASay()
    {
        typeof(TenantSeederController).Should().BeDecoratedWith<AllowWithoutHrmsProductAttribute>();
    }
}
