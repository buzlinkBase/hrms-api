using Hrms.adms.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hrms.adms.Filters;

/// <summary>
/// Declarative per-action permission gate — checks the caller's "permission" JWT claims
/// (ClaimsPrincipal.HasAnyPermission, see Hrms.adms.Extensions.HttpRequestExtensions; the claims
/// themselves are minted by AuthApi's JwtService from the tenant's Permission catalog, same
/// issuer/audience/signing key this service already validates) against one or more permission
/// codes, any-of. 403s with the codes it needed if none match.
///
/// Mirrors hrms-api's Hrms.Api.Filters.RequirePermissionAttribute -- duplicated here rather than
/// shared, since hrms-adms-api doesn't reference hrms-api's web project.
///
/// Usage: [HttpGet] [RequirePermission("Biometric Setup:View")] public async Task&lt;...&gt; Get(...)
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
