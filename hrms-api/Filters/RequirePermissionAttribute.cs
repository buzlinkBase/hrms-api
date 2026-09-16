using Hrms.Api.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hrms.Api.Filters;

/// <summary>
/// Declarative per-action permission gate — checks the caller's "permission" JWT claims
/// (ClaimsPrincipal.HasAnyPermission, see Hrms.Api.Extensions.HttpRequestExtensions; the claims
/// themselves are minted by AuthApi's JwtService from the tenant's Permission catalog) against
/// one or more permission codes, any-of. 403s with the codes it needed if none match.
///
/// Usage: [HttpGet] [RequirePermission("Organization Setup:View")] public async Task&lt;...&gt; Get(...)
///
/// Unlike EmployeeOnlyRestrictionFilter (registered globally, restricts one specific account
/// shape everywhere), this is applied per action/controller where real enforcement is wanted —
/// most controllers in this codebase don't use it yet and rely solely on the
/// RequireAuthenticatedUser fallback policy (see ServiceRegitrations.cs).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequirePermissionAttribute(params string[] codes) : Attribute, IAsyncActionFilter
{
    public string[] Codes => codes;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.User.HasAnyPermission(codes))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"Requires one of: {string.Join(", ", codes)}",
            })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        await next();
    }
}
