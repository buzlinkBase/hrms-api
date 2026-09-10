using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hrms.Api.Filters;

/// <summary>
/// Backend mirror of the frontend's Employee-only portal restriction (see
/// rootRoute.beforeLoad in hrms-ui-onepunch). A caller whose role set for the current tenant is
/// exactly ["Employee"] (no Admin/Owner/Member/Custom role alongside it) can only reach
/// self-service ("/me/*") endpoints -- everything else is 403. Role claims are embedded in the
/// JWT at mint time by AuthApi's JwtService; a caller with no role claims at all (e.g. tokens
/// minted before this existed) is left alone, since there's nothing to restrict against.
/// </summary>
public class EmployeeOnlyRestrictionFilter : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var roles = context.HttpContext.User
            .FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();
        var isEmployeeOnly = roles.Count > 0 && roles.All(r => r == "Employee");

        if (isEmployeeOnly && !IsSelfServicePath(context.HttpContext.Request.Path))
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
