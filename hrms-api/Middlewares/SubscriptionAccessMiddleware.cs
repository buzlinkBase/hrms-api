using Hrms.Api.Filters;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.Api.Middlewares;

/// <summary>
/// Read-only subscriptions (trial ended, suspended for non-payment, canceled) can sign in, view
/// and export, but not change anything: every POST/PUT/PATCH/DELETE from a signed-in company user
/// is refused with 403 ProblemDetails type "subscription-read-only", unless the endpoint is
/// marked <see cref="AllowWhenReadOnlyAttribute"/>. Enforced here on the backend -- hiding
/// buttons in the UI isn't enforcement. Runs after TenantDatabaseMiddleware (the company is known).
/// Anonymous and service-to-service calls aren't company users and pass; so do the SignalR hubs.
/// </summary>
public class SubscriptionAccessMiddleware
{
    private readonly RequestDelegate _next;
    public SubscriptionAccessMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider, IEntitlementProvider entitlements)
    {
        if (IsWrite(context.Request.Method)
            && context.User.Identity?.IsAuthenticated == true
            && !context.Request.Path.StartsWithSegments("/hubs")
            && tenantProvider.TenantId != Guid.Empty
            && context.GetEndpoint()?.Metadata.GetMetadata<AllowWhenReadOnlyAttribute>() == null)
        {
            var snapshot = await entitlements.GetAsync(tenantProvider.TenantId, context.RequestAborted);
            if (snapshot.IsReadOnly)
            {
                throw PlanRestrictionException.ReadOnly(
                    "Your subscription is read-only, so changes can't be saved. Choose a plan in Billing to continue.");
            }
        }

        await _next(context);
    }

    private static bool IsWrite(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}
