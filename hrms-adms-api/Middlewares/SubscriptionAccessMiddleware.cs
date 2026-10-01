using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.adms.Middlewares;

/// <summary>
/// The lapsed-subscription rule (<see cref="SubscriptionAccessPolicy"/>) for the device admin
/// screens: the owner and billing admins keep read-only access; every other member of the company
/// is locked out. The device protocol (/iclock) is anonymous and passes -- its punches are governed
/// by PunchAcceptanceService instead. Mirrors hrms-api's SubscriptionAccessMiddleware.
/// </summary>
public class SubscriptionAccessMiddleware
{
    private readonly RequestDelegate _next;
    public SubscriptionAccessMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider, IEntitlementProvider entitlements)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && !context.Request.Path.StartsWithSegments("/iclock")
            && tenantProvider.TenantId != Guid.Empty)
        {
            var snapshot = await entitlements.GetAsync(tenantProvider.TenantId, context.RequestAborted);
            var decision = SubscriptionAccessPolicy.Decide(
                snapshot, context.Request.Method, BillingAccess.IsBillingUser(context.User));
            if (SubscriptionAccessPolicy.Refusal(decision) is { } refusal) throw refusal;
        }

        await _next(context);
    }
}
