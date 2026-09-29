using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Core.Services.Security;
using Hrms.Infrastructure;

namespace Hrms.Api.Middlewares;

/// <summary>
/// Fills the request's <see cref="ConfidentialAccessScope"/> before any controller runs a query,
/// so the global query filter hides confidential employees from callers without clearance, or only
/// the classes of record a partial clearance (Operations / Entries) doesn't cover.
/// Runs after authentication and TenantDatabaseMiddleware (the company and its connection are
/// known by then). Cleared callers, the Owner and anonymous requests are left unrestricted and
/// cost nothing.
/// </summary>
public class ConfidentialAccessMiddleware
{
    private readonly RequestDelegate _next;
    public ConfidentialAccessMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ConfidentialAccessScope scope, HrmsContext db, ITenantProvider tenantProvider)
    {
        var user = context.User;
        var tenantId = tenantProvider.TenantId;
        var isHubRequest = context.Request.Path.StartsWithSegments("/hubs"); // no data queries there

        if (!isHubRequest &&
            user.Identity?.IsAuthenticated == true &&
            tenantId != Guid.Empty &&
            !user.HasConfidentialClearance() &&
            user.GetUserId() is { } userId)
        {
            // An endpoint working on one class of data only honours the grant for it -- see
            // ConfidentialDataAttribute.
            var grants = user.GetConfidentialGrants();
            if (context.GetEndpoint()?.Metadata.GetMetadata<ConfidentialDataAttribute>() is { } dataScope)
                grants = grants.NarrowTo(dataScope.DataClass);
            await scope.InitializeAsync(db, tenantId, userId, user.GetUserClaim("email"), grants, context.RequestAborted);
        }

        await _next(context);
    }
}
