using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hrms.Api.Filters;

/// <summary>
/// Backend mirror of the frontend's Employee-only portal restriction (see
/// rootRoute.beforeLoad in hrms-ui-onepunch). A caller whose role set for the current tenant is
/// exactly ["Employee"] (no Admin/Owner/Member/Custom role alongside it) can only *write* outside
/// self-service ("/me/*") endpoints -- POST/PUT/PATCH/DELETE elsewhere is 403. GET is left alone
/// everywhere: several portal pages legitimately call non-"/me/*" endpoints for read-only
/// reference/lookup data that isn't employee-specific (e.g. My Daily Time Record resolving shift
/// names via GET /api/v1/timeshifts, or the Leave/Loan filing forms reading leave types and
/// deduction types) -- blocking those GETs too broke that, since this filter was only ever meant
/// to stop an employee-only account from reaching admin CRUD actions, not from reading the same
/// shared config data every portal page already needs to render. Role claims are embedded in the
/// JWT at mint time by AuthApi's JwtService; a caller with no role claims at all (e.g. tokens
/// minted before this existed) is left alone, since there's nothing to restrict against.
/// </summary>
public class EmployeeOnlyRestrictionFilter : IAsyncActionFilter
{
    // Employee-only accounts stay restricted here even while TEMP-ALLOW-ALL bypasses every other
    // permission check elsewhere (HttpRequestExtensions.HasPermission, UserMembership) --
    // explicitly asked to keep this one gate real.
    private static readonly bool TempAllowAll = false;

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (TempAllowAll) return next();

        var roles = context.HttpContext.User
            .FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();
        var isEmployeeOnly = roles.Count > 0 && roles.All(r => r == "Employee");
        var isRead = HttpMethods.IsGet(context.HttpContext.Request.Method);

        if (isEmployeeOnly && !isRead && !IsSelfServicePath(context.HttpContext.Request.Path))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "Employee-only accounts can only access self-service endpoints.",
            })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return Task.CompletedTask;
        }

        return next();
    }

    private static bool IsSelfServicePath(PathString path) =>
        path.Value?.Contains("/me/", StringComparison.OrdinalIgnoreCase) == true ||
        path.Value?.EndsWith("/me", StringComparison.OrdinalIgnoreCase) == true;
}
