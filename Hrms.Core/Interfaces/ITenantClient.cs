using Refit;
namespace Hrms.Core.Interfaces;

// Tenant Service calls made on the caller's behalf -- the caller's bearer token and X-Tenant-ID
// are forwarded by header propagation (see ServiceRegitrations), so Tenant Service resolves the
// tenant and checks the caller's own membership itself.
public interface ITenantClient
{
    // Renames the caller's current company everywhere Tenant/Auth Service keep a copy of its name
    // (tenant row, memberships, access-token claim, invitations). See TenantsController.RenameCurrent.
    [Put("/api/v1/tenants/current/name")]
    Task RenameCurrentAsync([Query] string name, CancellationToken token = default);

    // Keeps the console-facing contact details (address, contact number) Tenant Service holds in
    // line with Company Info. See TenantsController.UpdateCurrentProfile.
    [Put("/api/v1/tenants/current/profile")]
    Task UpdateCurrentProfileAsync([Body] TenantProfileUpdate profile, CancellationToken token = default);
}

public sealed record TenantProfileUpdate(string? Address, string? Contact);
