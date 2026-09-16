using System.Security.Claims;
using Hrms.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace hrms.test.FilterTests;

/// <summary>
/// RequirePermissionAttribute — the declarative per-action permission gate applied to
/// controllers as they get real enforcement (see WorkSchedulePlansController for the first,
/// hand-written precedent this generalizes). Any-of match against the caller's "permission"
/// JWT claims.
/// </summary>
public class RequirePermissionAttributeTests
{
    private static ActionExecutingContext BuildContext(params string[] permissions)
    {
        var httpContext = new DefaultHttpContext();
        if (permissions.Length > 0)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(permissions.Select(p => new Claim("permission", p))));
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());
    }

    private static ActionExecutionDelegate NextSpy(ActionContext actionContext, Action onCalled) => () =>
    {
        onCalled();
        var executed = new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object());
        return Task.FromResult(executed);
    };

    [Fact]
    public async Task Allows_WhenCallerHoldsTheRequiredCode()
    {
        var context = BuildContext("Organization Setup:View");
        var nextCalled = false;
        var filter = new RequirePermissionAttribute("Organization Setup:View");

        await filter.OnActionExecutionAsync(context, NextSpy(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task Allows_WhenCallerHoldsAnyOfMultipleRequiredCodes()
    {
        var context = BuildContext("Organization Setup:Create");
        var nextCalled = false;
        var filter = new RequirePermissionAttribute("Organization Setup:View", "Organization Setup:Create");

        await filter.OnActionExecutionAsync(context, NextSpy(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Forbids_WhenCallerHoldsNoPermissionsAtAll()
    {
        var context = BuildContext();
        var filter = new RequirePermissionAttribute("Organization Setup:View");

        await filter.OnActionExecutionAsync(context, NextSpy(context, () => { }));

        context.Result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)context.Result!).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Forbids_WhenCallerHoldsAnUnrelatedPermission()
    {
        var context = BuildContext("Workforce Setup:View");
        var filter = new RequirePermissionAttribute("Organization Setup:View");

        await filter.OnActionExecutionAsync(context, NextSpy(context, () => { }));

        context.Result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)context.Result!).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ForbiddenResponse_NamesTheRequiredCodes()
    {
        var context = BuildContext();
        var filter = new RequirePermissionAttribute("Organization Setup:View", "Organization Setup:Create");

        await filter.OnActionExecutionAsync(context, NextSpy(context, () => { }));

        var problem = (ProblemDetails)((ObjectResult)context.Result!).Value!;
        problem.Detail.Should().Contain("Organization Setup:View").And.Contain("Organization Setup:Create");
    }
}
