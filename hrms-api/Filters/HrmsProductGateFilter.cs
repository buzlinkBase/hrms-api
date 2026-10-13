using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Onepunch.Common.Lib.Security;
using Onepunch.Common.Web.Subscriptions;

namespace Hrms.Api.Filters;

/// <summary>
/// Marks a controller or action that must keep working when the account's plan doesn't include the
/// HRMS system -- e.g. tenant provisioning, which runs before anyone could have a say in the plan.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowWithoutHrmsProductAttribute : Attribute;

/// <summary>
/// Global gate: every HRMS endpoint needs the plan to include the HRMS system (hrms.core), so
/// switching it off in the license takes effect for the whole API -- the Owner included, who passes
/// every permission check but cannot use a system the plan doesn't include. Anonymous endpoints
/// (service-to-service, api-key protected) and those marked <see cref="AllowWithoutHrmsProductAttribute"/>
/// are left alone. Refuses with the same 403 "plan-required" as [RequireFeature].
/// </summary>
public class HrmsProductGateFilter : IAsyncActionFilter
{
    private static readonly RequireProductAttribute Gate = new(PermissionProducts.Hrms);

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var exempt = context.ActionDescriptor.EndpointMetadata
            .Any(m => m is IAllowAnonymous or AllowWithoutHrmsProductAttribute);

        return exempt ? next() : Gate.OnActionExecutionAsync(context, next);
    }
}
