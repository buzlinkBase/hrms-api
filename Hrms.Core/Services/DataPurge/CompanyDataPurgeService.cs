using System.Security.Cryptography;
using Hrms.Core.Services.Storage;
using Hrms.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.Core.Services.DataPurge;

/// <summary>What the purge steps share: the company, the catalog of its tables, and what each step reports.</summary>
public sealed class CompanyPurgeContext(HrmsContext db, Guid tenantId, DateTime nowUtc)
{
    public HrmsContext Db { get; } = db;
    public Guid TenantId { get; } = tenantId;
    public DateTime NowUtc { get; } = nowUtc;
    public CompanyDataCatalog Catalog { get; set; } = null!;

    /// <summary>Set by a step that can't go on; nothing after it runs. A failure before the delete step
    /// deletes nothing.</summary>
    public string? Error { get; private set; }
    public bool Failed => Error != null;
    public void Fail(string error) => Error ??= error;

    /// <summary>Whether the company had payroll / employee / attendance records, which must be archived before they go.</summary>
    public bool HadComplianceRows { get; set; }
    public DateTime? LastEntryUtc { get; set; }
    public string? ArchiveKey { get; set; }
    public string? ArchiveSha256 { get; set; }
    public long RowsArchived { get; set; }
    public long RowsDeleted { get; set; }
    public int FilesDeleted { get; set; }
}

/// <summary>
/// One link of the purge chain. Run in <see cref="Order"/>; a step that calls
/// <see cref="CompanyPurgeContext.Fail"/> stops the chain, so the order is also the safety: nothing is
/// deleted unless the company is locked and its records are archived and verified.
/// </summary>
public interface ICompanyPurgeStep
{
    int Order { get; }
    Task RunAsync(CompanyPurgeContext context, CancellationToken token);
}

/// <summary>Refuses to touch a company that is not read-only -- one that paid in the meantime, or whose
/// status couldn't be confirmed.</summary>
public sealed class RequireLockedStep(IEntitlementProvider entitlements) : ICompanyPurgeStep
{
    public int Order => 10;

    public async Task RunAsync(CompanyPurgeContext context, CancellationToken token)
    {
        var snapshot = await entitlements.GetAsync(context.TenantId, token);
        if (!snapshot.IsReadOnly)
            context.Fail($"The company isn't read-only (status {snapshot.Status}); nothing was deleted.");
    }
}

/// <summary>
/// Writes the company's records to a zip in the archive folder of the file storage, reads it back and
/// checks its SHA-256, then checks the database still holds exactly the rows that went in. Any
/// mismatch removes the new archive and fails the run -- the next attempt starts clean.
/// </summary>
public sealed class ArchiveRecordsStep(IFileStorage storage, CompanyArchiveWriter writer) : ICompanyPurgeStep
{
    public int Order => 20;

    public async Task RunAsync(CompanyPurgeContext context, CancellationToken token)
    {
        var database = context.Db.Database;
        await database.OpenConnectionAsync(token);
        try
        {
            var connection = database.GetDbConnection();
            long compliance = 0;
            foreach (var table in context.Catalog.ToArchive.Where(t => t.Kind == CompanyTableKind.Compliance))
                compliance += await CountAsync(connection, table, context.TenantId, token);

            // Nothing to keep (an earlier run already archived and removed it).
            if (compliance == 0) return;
            context.HadComplianceRows = true;

            await using var file = new FileStream(
                Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip"),
                FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous);

            var summary = await writer.WriteAsync(connection, context.Catalog, context.TenantId, file, context.NowUtc, token);
            await file.FlushAsync(token);

            file.Position = 0;
            var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(file, token));
            file.Position = 0;
            var key = StorageKeys.ForArchive(context.TenantId, $"company-archive-{context.NowUtc:yyyyMMddHHmmss}.zip");
            await storage.UploadAsync(new FileUpload(key, file, "application/zip"), token);

            if (!await IsStoredIntactAsync(key, sha256, token))
            {
                await storage.DeleteAsync(key, token);
                context.Fail("The archive couldn't be read back intact; nothing was deleted.");
                return;
            }

            // The company is read-only, but check anyway: rows that arrived while the archive was
            // being written would be deleted without being in it.
            long now = 0;
            foreach (var table in context.Catalog.ToArchive)
                now += await CountAsync(connection, table, context.TenantId, token);
            if (now != summary.TotalRows)
            {
                await storage.DeleteAsync(key, token);
                context.Fail("The data changed while it was being archived; nothing was deleted. It will be tried again.");
                return;
            }

            context.ArchiveKey = key;
            context.ArchiveSha256 = sha256;
            context.RowsArchived = summary.TotalRows;
            context.LastEntryUtc = summary.LastEntryUtc;
        }
        finally
        {
            await database.CloseConnectionAsync();
        }
    }

    private async Task<bool> IsStoredIntactAsync(string key, string sha256, CancellationToken token)
    {
        await using var stored = await storage.OpenReadAsync(key, token);
        return stored != null
            && string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(stored, token)), sha256, StringComparison.Ordinal);
    }

    private static async Task<long> CountAsync(System.Data.Common.DbConnection connection, CompanyTable table, Guid tenantId, CancellationToken token)
    {
        await using var command = CompanyDataCatalog.CreateCommand(connection, table.CountSql, tenantId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Deletes the company's rows, children before parents, in batches. The company's own row stays.
/// Won't run for a company that had records but has no verified archive of them.
/// </summary>
public sealed class DeleteRecordsStep : ICompanyPurgeStep
{
    private const int BatchSize = 5000;

    public int Order => 30;

    public async Task RunAsync(CompanyPurgeContext context, CancellationToken token)
    {
        if (context.HadComplianceRows && context.ArchiveKey == null)
        {
            context.Fail("There is no verified archive of this company's records; nothing was deleted.");
            return;
        }

        var database = context.Db.Database;
        var mysql = database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;
        await database.OpenConnectionAsync(token);
        try
        {
            // MySQL checks a foreign key as each row goes, so a table that refers to itself can't be
            // emptied in one pass; the whole company is going anyway.
            if (mysql) await database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0", token);
            foreach (var table in context.Catalog.ToDelete)
            {
                var sql = CompanyDataCatalog.ForEf(table.DeleteSql);
                int deleted;
                do
                {
                    deleted = await database.ExecuteSqlRawAsync(mysql ? $"{sql} LIMIT {BatchSize}" : sql, [context.TenantId], token);
                    context.RowsDeleted += deleted;
                } while (mysql && deleted >= BatchSize);
            }
        }
        finally
        {
            if (mysql) await database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1", CancellationToken.None);
            await database.CloseConnectionAsync();
        }
    }
}

/// <summary>Deletes the company's uploaded files (logos, photos, documents). The archive lives elsewhere.</summary>
public sealed class DeleteFilesStep(IFileStorage storage) : ICompanyPurgeStep
{
    public int Order => 40;

    public async Task RunAsync(CompanyPurgeContext context, CancellationToken token) =>
        context.FilesDeleted = await storage.DeleteByPrefixAsync(StorageKeys.TenantPrefix(context.TenantId), token);
}

/// <summary>
/// Removes a locked company's data from the live database and file storage after archiving what must
/// be kept (<see cref="CompanyPurgeStage.ArchiveAndPurge"/>), and later destroys the archive itself
/// (<see cref="CompanyPurgeStage.DestroyArchive"/>). Never throws for a failed run: it returns the
/// failure for Tenant Service to record and ask again tomorrow. Safe to run twice.
/// </summary>
public sealed class CompanyDataPurgeService(
    HrmsContext db,
    IFileStorage storage,
    IEnumerable<ICompanyPurgeStep> steps,
    ILogger<CompanyDataPurgeService> logger)
{
    public async Task<CompanyDataPurged> RunAsync(PurgeCompanyData request, CancellationToken token)
    {
        var context = new CompanyPurgeContext(db, request.TenantId, DateTime.UtcNow);
        try
        {
            if (request.TenantId == Guid.Empty) context.Fail("A purge needs a company.");
            else if (request.Stage == CompanyPurgeStage.DestroyArchive)
                context.FilesDeleted = await storage.DeleteByPrefixAsync(StorageKeys.ArchivePrefix(request.TenantId), token);
            else await RunStepsAsync(context, token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Data purge [{Tenant}] {Stage} failed", request.TenantId, request.Stage);
            context.Fail($"{ex.GetType().Name}: {ex.Message}");
        }

        if (context.Failed)
            logger.LogWarning("Data purge [{Tenant}] {Stage} stopped: {Error}", request.TenantId, request.Stage, context.Error);
        else
            logger.LogInformation(
                "Data purge [{Tenant}] {Stage} done: {Archived} rows archived, {Deleted} rows and {Files} files deleted",
                request.TenantId, request.Stage, context.RowsArchived, context.RowsDeleted, context.FilesDeleted);

        return new CompanyDataPurged
        {
            OwnerUserId = request.OwnerUserId,
            SubscriptionId = request.SubscriptionId,
            TenantId = request.TenantId,
            Stage = request.Stage,
            Succeeded = !context.Failed,
            Error = context.Error,
            LastEntryUtc = context.LastEntryUtc,
            ArchiveKey = context.ArchiveKey,
            ArchiveSha256 = context.ArchiveSha256,
            RowsArchived = context.RowsArchived,
            RowsDeleted = context.RowsDeleted,
            FilesDeleted = context.FilesDeleted,
            CompletedAtUtc = DateTime.UtcNow,
        };
    }

    private async Task RunStepsAsync(CompanyPurgeContext context, CancellationToken token)
    {
        context.Catalog = CompanyDataCatalog.Build(db.Model, db.GetService<ISqlGenerationHelper>());
        foreach (var step in steps.OrderBy(s => s.Order))
        {
            await step.RunAsync(context, token);
            if (context.Failed) return;
        }
    }
}

public static class CompanyDataPurgeRegistration
{
    /// <summary>The purge chain, in the order each step's <c>Order</c> gives.</summary>
    public static void AddCompanyDataPurge(IServiceCollection services)
    {
        services.AddSingleton<CompanyArchiveWriter>();
        services.AddScoped<ICompanyPurgeStep, RequireLockedStep>();
        services.AddScoped<ICompanyPurgeStep, ArchiveRecordsStep>();
        services.AddScoped<ICompanyPurgeStep, DeleteRecordsStep>();
        services.AddScoped<ICompanyPurgeStep, DeleteFilesStep>();
    }
}
