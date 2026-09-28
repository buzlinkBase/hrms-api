using Hrms.adms.Insfrastructure;
using Onepunch.AuditEngine;
using Onepunch.AuditEngine.AspNetCore;
using Onepunch.AuditEngine.EntityFrameworkCore;
using Onepunch.AuditEngine.MassTransit;

namespace Hrms.adms.Services.Auditing;

/// <summary>
/// Biometric device administration trail: device add/edit/delete, queued device commands
/// (enroll, sync, delete-from-device, reboot, ...) and template deletions are recorded in
/// AdmsContext's own AuditEvents table, in the same transaction as the change, and forwarded
/// through the EF outbox to hrms-api's combined Security > Audit view.
/// </summary>
public static class AdmsAuditRegistration
{
    public const string SourceName = "adms-api";

    /// <param name="configuration">"AuditEngine:Enabled" = false turns capture off, e.g. to deploy
    /// before the AuditEvents table is migrated.</param>
    public static IServiceCollection AddAdmsAuditTrail(this IServiceCollection services, IConfiguration configuration) =>
        services.AddAuditEngine(o =>
        {
            o.Options.SourceName = SourceName;
            o.Options.Enabled = configuration.GetValue("AuditEngine:Enabled", true);
            o.UseEntityFrameworkCore<AdmsContext>();
            o.UseAspNetCore(claims => claims.OrganizationResolver = CompanyOf);
            o.ForwardToMassTransit(f => f.OrganizationHeaderName = "X-Tenant-ID");
        });

    // The request's company (JWT claim or X-Tenant-ID header), or the consumed message's company
    // for background work -- both surface through ITenantProvider.
    private static string? CompanyOf(IServiceProvider services) =>
        services.GetService<ITenantProvider>()?.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId.ToString()
            : null;
}
