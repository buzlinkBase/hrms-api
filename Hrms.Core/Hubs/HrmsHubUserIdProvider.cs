using Microsoft.AspNetCore.SignalR;

namespace Hrms.Core.Hubs;

// Enables Clients.User(userId) targeting across every hub in this app (SignalR has exactly one
// IUserIdProvider per app, not per-hub) -- mirrors tenantstore's proven TenantHubUserIdProvider
// (Services\AuthApi\Onepunch.Auth.Core\Hubs\TenantHub.cs), reading the same "sub" claim the JWT
// already carries. An unauthenticated connection simply resolves to null here, so
// Clients.User(...) never delivers to it -- no behavior change for NotificationHub's existing
// anonymous/group-based AI job-progress usage.
public class HrmsHubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("sub")?.Value
        ?? connection.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
}
