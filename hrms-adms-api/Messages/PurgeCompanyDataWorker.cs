using Hrms.adms.Insfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Onepunch.AuditEngine.EntityFrameworkCore;
using Onepunch.Common.Lib.DTO;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.adms.Messages;

/// <summary>
/// Tenant Service's data purge reached a locked company: remove what the device service holds for it --
/// its devices, device users, punches and, above all, the biometric templates. The payroll-relevant
/// copy of the punches is in hrms-api's database, which archives it before deleting; this service keeps
/// nothing the law asks employers to retain, so it deletes outright and stays silent (hrms-api's
/// <c>CompanyDataPurged</c> is the one answer Tenant Service waits for).
/// Does nothing for a company that is not read-only. Tenant Service asks again each day until the whole
/// purge is done, so a skipped or failed run is retried.
/// An operator's erasure of a (test) account (<see cref="CompanyPurgeStage.HardDelete"/>, approved by two
/// operators) skips the read-only check -- the company may still be live -- and takes the company's
/// audit trail too.
/// </summary>
public class PurgeCompanyDataWorker(
    AdmsContext db,
    IEntitlementProvider entitlements,
    ITenantProvider tenantProvider,
    ILogger<PurgeCompanyDataWorker> logger) : IConsumer<PurgeCompanyData>
{
    public async Task Consume(ConsumeContext<PurgeCompanyData> context)
    {
        var request = context.Message;
        if (request.Stage is not (CompanyPurgeStage.ArchiveAndPurge or CompanyPurgeStage.HardDelete)) return;
        var erasing = request.Stage == CompanyPurgeStage.HardDelete;
        if (request.TenantId == Guid.Empty || request.TenantId != tenantProvider.TenantId)
        {
            logger.LogWarning("Data purge for {Tenant} ignored: it doesn't match the tenant header", request.TenantId);
            return;
        }

        if (!erasing)
        {
            var snapshot = await entitlements.GetAsync(request.TenantId, context.CancellationToken);
            if (!snapshot.IsReadOnly)
            {
                logger.LogWarning("Data purge for {Tenant} skipped: the company isn't read-only (status {Status})", request.TenantId, snapshot.Status);
                return;
            }
        }

        var deleted = await AdmsCompanyData.DeleteAsync(db, request.TenantId, context.CancellationToken);
        if (erasing)
        {
            deleted += await AdmsCompanyData.DeleteAuditTrailAsync(db, request.TenantId, context.CancellationToken);
            await entitlements.InvalidateAsync([request.TenantId]);
        }
        logger.LogInformation("Data purge [{Tenant}] {Stage}: {Rows} device-service rows deleted", request.TenantId, request.Stage, deleted);
    }
}

/// <summary>Deletes every row of one company from the device-service database.</summary>
public static class AdmsCompanyData
{
    private const int BatchSize = 5000;

    /// <summary>The tables with a TenantId column, as (table, column) pairs.</summary>
    public static IReadOnlyList<(string Table, string? Schema, string Column)> TenantTables(IModel model) =>
        model.GetEntityTypes()
            .Where(t => t.GetTableName() != null)
            .Select(t => (Type: t, Property: t.FindProperty("TenantId")))
            .Where(x => x.Property != null && x.Property.ClrType == typeof(Guid))
            .Select(x => (Table: x.Type.GetTableName()!, Schema: x.Type.GetSchema(),
                Column: x.Property!.GetColumnName(StoreObjectIdentifier.Table(x.Type.GetTableName()!, x.Type.GetSchema()))!))
            .Distinct()
            .OrderBy(x => x.Table, StringComparer.Ordinal)
            .ToList();

    public static async Task<long> DeleteAsync(AdmsContext db, Guid tenantId, CancellationToken token)
    {
        var sql = db.GetService<ISqlGenerationHelper>();
        var database = db.Database;
        var mysql = database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;
        long total = 0;

        await database.OpenConnectionAsync(token);
        try
        {
            // The device tables refer to each other; the whole company is going, so order doesn't matter.
            if (mysql) await database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0", token);
            foreach (var (table, schema, column) in TenantTables(db.Model))
            {
                var statement = $"DELETE FROM {sql.DelimitIdentifier(table, schema)} WHERE {sql.DelimitIdentifier(column)} = {{0}}";
                int deleted;
                do
                {
                    deleted = await database.ExecuteSqlRawAsync(mysql ? $"{statement} LIMIT {BatchSize}" : statement, [tenantId], token);
                    total += deleted;
                } while (mysql && deleted >= BatchSize);
            }
        }
        finally
        {
            if (mysql) await database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1", CancellationToken.None);
            await database.CloseConnectionAsync();
        }
        return total;
    }

    /// <summary>The company's audit trail (an erasure only; the hash chain is per company, so no other
    /// company's chain is touched).</summary>
    public static Task<int> DeleteAuditTrailAsync(AdmsContext db, Guid tenantId, CancellationToken token)
    {
        var organization = tenantId.ToString();
        return db.Set<AuditEventRecord>().Where(a => a.OrganizationId == organization).ExecuteDeleteAsync(token);
    }
}

public class PurgeCompanyDataWorkerDefinition : ConsumerDefinition<PurgeCompanyDataWorker>
{
    public PurgeCompanyDataWorkerDefinition()
    {
        EndpointName = "adms-purge-company-data-que";
        ConcurrentMessageLimit = 1;
    }
}
