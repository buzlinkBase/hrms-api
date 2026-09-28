using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Onepunch.AuditEngine;
using Onepunch.AuditEngine.AspNetCore;
using Onepunch.AuditEngine.EntityFrameworkCore;

namespace Hrms.Core.Services.Auditing;

/// <summary>
/// hrms-api's audit trail: changes to [Audited] entities (employees, applications, payroll/DTR
/// batches, approval workflows) plus every approve/decline are recorded in each company's own
/// AuditEvents table, in the same transaction as the change. The same table also holds the
/// copies AuditReplicaWorker receives from AuthApi (sign-ins) and TenantApi (roles/members), so
/// Security > Audit reads one combined list. hrms-api is the collector, so it forwards nothing.
/// </summary>
public static class HrmsAuditRegistration
{
    public const string SourceName = "hrms-api";

    /// <param name="configuration">"AuditEngine:Enabled" = false turns capture off, e.g. to deploy
    /// before the AuditEvents table is migrated.</param>
    public static IServiceCollection AddHrmsAuditTrail(this IServiceCollection services, IConfiguration configuration) =>
        services.AddAuditEngine(o =>
        {
            o.Options.SourceName = SourceName;
            o.Options.Enabled = configuration.GetValue("AuditEngine:Enabled", true);
            // Government IDs and bank numbers: that they changed is recorded, not their values.
            foreach (var property in new[] { "TIN", "SSSNo", "PHICNo", "HDMFNo", "BankNo" })
                o.Options.RedactedProperties.Add(property);
            o.UseEntityFrameworkCore<HrmsContext>();
            o.UseAspNetCore(claims => claims.OrganizationResolver = CompanyOf);
        });

    // The request's company (JWT claim or X-Tenant-ID header), or the consumed message's company
    // for background work -- both surface through ITenantProvider.
    private static string? CompanyOf(IServiceProvider services) =>
        services.GetService<ITenantProvider>()?.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId.ToString()
            : null;
}
