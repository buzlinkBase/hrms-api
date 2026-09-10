using System.Security.Claims;
using Hrms.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace hrms.test.FilterTests;

/// <summary>
/// EmployeeOnlyRestrictionFilter — the backend mirror of the frontend's Employee-only portal
/// route guard. Employee-only callers may only *write* (POST/PUT/PATCH/DELETE) under "/me/*";
/// GET is allowed everywhere, since portal pages read shared reference data (time shifts, leave
/// types, deduction types, etc.) from non-"/me/*" endpoints. A multi-role caller is never
/// restricted by this filter at all.
/// </summary>
public class EmployeeOnlyRestrictionFilterTests
{
    private static (ActionExecutingContext Context, Func<bool> WasNextCalled) BuildContext(
        string method, string path, params string[] roles)
    {
        var httpContext = new DefaultHttpContext
        {
            Request = { Method = method, Path = path },
        };
        if (roles.Length > 0)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r))));
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());

        return (context, () => true);
    }

    private static ActionExecutionDelegate NextSpy(ActionContext actionContext, Action onCalled) => () =>
    {
        onCalled();
        var executed = new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object());
        return Task.FromResult(executed);
    };

    [Fact]
    public async Task AllowsGetOutsideMe_ForEmployeeOnlyCaller()
    {
        var (context, _) = BuildContext("GET", "/api/v1/timeshifts", "Employee");
        var nextCalled = false;
        var filter = new EmployeeOnlyRestrictionFilter();

        await filter.OnActionExecutionAsync(context, NextSpy(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task BlocksPostOutsideMe_ForEmployeeOnlyCaller()
    {
        var (context, _) = BuildContext("POST", "/api/v1/timeshifts", "Employee");
        var filter = new EmployeeOnlyRestrictionFilter();

        await filter.OnActionExecutionAsync(context, NextSpy(context, () => { }));

        context.Result.Should().BeOfType<ObjectResult>();
        ((ObjectResult)context.Result!).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task AllowsWriteUnderMe_ForEmployeeOnlyCaller()
    {
        var (context, _) = BuildContext("POST", "/api/v1/me/leave-applications", "Employee");
        var nextCalled = false;
        var filter = new EmployeeOnlyRestrictionFilter();

        await filter.OnActionExecutionAsync(context, NextSpy(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task AllowsAnyMethod_ForMultiRoleCaller()
    {
        var (context, _) = BuildContext("DELETE", "/api/v1/employees/123", "Employee", "Admin");
        var nextCalled = false;
        var filter = new EmployeeOnlyRestrictionFilter();

        await filter.OnActionExecutionAsync(context, NextSpy(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task AllowsAnyMethod_ForCallerWithNoRoleClaims()
    {
        var (context, _) = BuildContext("DELETE", "/api/v1/employees/123");
        var nextCalled = false;
        var filter = new EmployeeOnlyRestrictionFilter();

        await filter.OnActionExecutionAsync(context, NextSpy(context, () => nextCalled = true));

        nextCalled.Should().BeTrue();
    }
}
