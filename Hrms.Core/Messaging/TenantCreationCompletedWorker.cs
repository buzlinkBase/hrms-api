using MassTransit;
using Microsoft.Extensions.Configuration;
using Onepunch.Common.Lib.DbServices;
using Serilog;

namespace Hrms.Core.Messaging;

public class TenantCreationCompletedWorker : IConsumer<TenantCreationCompleted>
{
    private readonly IConfiguration _configuration;
    private readonly InstanceProvisioner _instanceProvisioner;
    private readonly DedicatedProvisioner _dedicatedProvisioner;

    public TenantCreationCompletedWorker(
        IConfiguration configuration,
        InstanceProvisioner instanceProvisioner,
        DedicatedProvisioner dedicatedProvisioner)
    {
        _configuration = configuration;
        _instanceProvisioner = instanceProvisioner;
        _dedicatedProvisioner = dedicatedProvisioner;
    }

    public Task Consume(ConsumeContext<TenantCreationCompleted> context)
    {
        var useDedicated = _configuration.GetValue<bool?>("Hris:DedicatedDatabase") ?? false;
        ITenantProvisioner provisioner = useDedicated 
            ? _dedicatedProvisioner 
            : _instanceProvisioner;

        return provisioner.ProvisionAsync(context);
    }
}


public interface ITenantProvisioner
{
    Task ProvisionAsync(ConsumeContext<TenantCreationCompleted> context);
}

// -------------------------------------------------------------
// Shared Instance Strategy: reuse the single configured HrmsConnection
// database for every tenant (no new physical database created).
// -------------------------------------------------------------
public class InstanceProvisioner : ITenantProvisioner
{
    private readonly IConfiguration _configuration;
    private readonly IMigrationService _migrationService;
    private readonly TenantConnectionStringInfo _tenantInfo;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUnitOfWorkService _uow;

    public InstanceProvisioner(
        IConfiguration configuration,
        IMigrationService migrationService,
        TenantConnectionStringInfo tenantInfo,
        ITenantProvider tenantProvider,
        IUnitOfWorkService uow
        )
    {
        _configuration = configuration;
        _migrationService = migrationService;
        _tenantInfo = tenantInfo;
        _tenantProvider = tenantProvider;
        _uow = uow;
    }

    public async Task ProvisionAsync(ConsumeContext<TenantCreationCompleted> context)
    {
        var message = context.Message;
        string connectionString = _configuration
            .GetConnectionString("HrmsConnection") ?? string.Empty;

        // Configure scoped tenant context directly
        _tenantInfo.TenantId = message.TenantId;
        _tenantInfo.ConnectionString = connectionString;
        _tenantProvider.SetTenantId(message.TenantId);

        // Execute migrations on shared DB
        //_migrationService.Migrate(connectionString);
        // Publish completion event (Flow C: consumed by Auth's HrDbCreatedWorker to push the
        // "tenant-added" SignalR notification back to the waiting client)
        await context.Publish(new HrisOrgProvisionedPayload
        {
            TenantId = message.TenantId,
            HrisOrgId = message.TenantId.ToString(),
            DatabaseName = "shared",
            Status = "Active", 
            ProvisionedAtUtc = DateTime.UtcNow
        }, context.CancellationToken);

        await _uow.CommitChangesAsync("", context.CancellationToken);
    }
}

// -------------------------------------------------------------
// Dedicated Infrastructure Strategy: create a physically separate database
// for this tenant via IDbService (SkySqlDbService creates it on the shared
// MySQL host today; swap to DigitalOceanDbService/RegisterDO for a truly
// separate cluster per tenant without touching this class).
// -------------------------------------------------------------
public class DedicatedProvisioner : ITenantProvisioner
{
    private readonly IConfiguration _configuration;
    private readonly IDbService _dbService;
    private readonly IMigrationService _migrationService;
    private readonly TenantConnectionStringInfo _tenantInfo;
    private readonly ITenantProvider _tenantProvider;

    public DedicatedProvisioner(
        IConfiguration configuration,
        IDbService dbService,
        IMigrationService migrationService,
        TenantConnectionStringInfo tenantInfo,
        ITenantProvider tenantProvider)
    {
        _configuration = configuration;
        _dbService = dbService;
        _migrationService = migrationService;
        _tenantInfo = tenantInfo;
        _tenantProvider = tenantProvider;
    }

    public async Task ProvisionAsync(ConsumeContext<TenantCreationCompleted> context)
    {
        var message = context.Message;
        string dbName = $"hrms_{message.TenantId:N}";
        var clusterId = _configuration["DigitalOcean:ClusterId"] ?? string.Empty;

        try
        {
            // CreateTenantDatabaseAsync is idempotent (CREATE DATABASE IF NOT EXISTS under
            // SkySqlDbService), so redelivered messages from MassTransit's retry policy are safe.
            var connectionModel = await _dbService.CreateTenantDatabaseAsync(clusterId, dbName);
            if (connectionModel == null)
            {
                throw new InvalidOperationException($"Failed to create tenant database '{dbName}'.");
            }

            // Hydrate scoped tenant state
            _tenantInfo.TenantId = message.TenantId;
            _tenantInfo.ConnectionString = connectionModel.ConnectionString;
            _tenantProvider.SetTenantId(message.TenantId);

            // Migrate database schema
            _migrationService.Migrate(connectionModel.ConnectionString);

            // Publish domain events via ConsumeContext pipeline
            await context.Publish(new ConnectionStringPayload
            {
                ConnectionString = connectionModel.ConnectionString,
                Environment = "Production",
                IsActive = true,
                Module = "hrms",
                ServiceOwner = "hrms",
                SchemaVersion = "1",
                TenantId = message.TenantId,
                ClusterId = clusterId,
                DatabaseName = dbName
            }, context.CancellationToken);

            await context.Publish(new SchemaVersionUpdatePayload
            {
                CurrentVersion = "1.0.0",
                Status = "Active",
                System = "HRIS",
                TenantId = message.TenantId,
            }, context.CancellationToken);

            // Flow C: consumed by Auth's HrDbCreatedWorker to push the "tenant-added" SignalR
            // notification back to the waiting client.
            await context.Publish(new HrisOrgProvisionedPayload
            {
                TenantId = message.TenantId,
                HrisOrgId = message.TenantId.ToString(),
                DatabaseName = dbName,
                Status = "Active",
                ProvisionedAtUtc = DateTime.UtcNow
            }, context.CancellationToken); 
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to provision dedicated database for Tenant {TenantId}", message.TenantId);
            throw; // Let MassTransit retry policy handle redelivery
        }
    }
}

// -------------------------------------------------------------
// Flow C entry point: consumes TenantCreationCompleted and delegates to whichever
// provisioning strategy is configured (Hris:DedicatedDatabase, default true).
// -------------------------------------------------------------

