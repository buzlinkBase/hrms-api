using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Onepunch.Common.Lib;
using Serilog;
using TenantStoreApi.Infrastructure;

namespace MigrationHrns;

public class MigrationRunner
{
    public async Task Runner(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var systemName = Environment.GetEnvironmentVariable("SYSTEM_NAME") ?? "HRIS";
        // all Tenant must have this version
        //run migration if tenant dont have this migration TargetVersion
        var TargetVersion = "1.0.1";
        if (string.IsNullOrEmpty(systemName))
        {
            Log.Information("SYSTEM_NAME env var not found. Skipping migration trigger.");
            return;
        }
        // 1. Update ONLY the target for the system being deployed
        await db.SchemaVersions
            .Where(sv => sv.System == systemName)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.TargetVersion, TargetVersion));

        // 2. Query only that system's pending migrations
        var pending = await db.SchemaVersions
            .Where(sv => sv.System == systemName && sv.CurrentVersion != sv.TargetVersion)
            .ToListAsync();
        try
        {
            // Target only the rows for THIS system that need an update
            var pendingMigrations = await db.SchemaVersions
                .Where(sv => sv.System == systemName && sv.CurrentVersion != sv.TargetVersion)
                .Select(sv => new
                {
                    sv.TenantId,
                    sv.TargetVersion,
                    sv.CurrentVersion,
                    sv.System,
                })
                .ToListAsync();

            Log.Information($"Found {pendingMigrations.Count} migrations for {systemName}");

            foreach (var m in pendingMigrations)
            {
                await publisher.Publish(new MigrateTenantDb
                {
                    TenantId = m.TenantId,
                    TargetVersion = m.TargetVersion,
                    CurrentVersion = m.CurrentVersion,
                    System = m.System
                }, context =>
                {
                    context.Headers.Set("X-Tenant-ID", m.TenantId.ToString());
                    context.CorrelationId = m.TenantId;
                });
                Log.Information($"[QUEUED] Tenant: {m.TenantId} to {m.TargetVersion}");
            }
            // IMPORTANT: Flush the bus before exiting
            // If you Exit(0) immediately, the message might still be in the local buffer
            var busControl = host.Services.GetRequiredService<IBusControl>();
            await busControl.StopAsync();
            Environment.Exit(0);
            Log.Information($"running migration console successfully");
        }
        catch (Exception ex)
        {
            Log.Error($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
