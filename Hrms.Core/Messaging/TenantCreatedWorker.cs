using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Core.Messaging;

public class TenantCreatedWorker : IConsumer<TenantCreatedPayload>
{
    private readonly IPublishEndpoint _publisher;
    private readonly IDbService _oceanDbService;
    private readonly IMigrationService _migrationService;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IServiceScopeFactory _factory;
    public TenantCreatedWorker(
        IPublishEndpoint publisher,
        IDbService digitalOceanDbService,
        IMigrationService migrationService,
        IConfiguration configuration,
        IHostEnvironment environment,
        IServiceScopeFactory factory)
    {
        _publisher = publisher;
        _oceanDbService = digitalOceanDbService;
        _migrationService = migrationService;
        _configuration = configuration;
        _environment = environment;
        _factory = factory;
    }

    public async Task Consume(ConsumeContext<TenantCreatedPayload> context)
    {
        var message = context.Message;
        string dbName = $"hrms_{message.TenantId:N}";
        var clusterId = _configuration["DigitalOcean:ClusterId"]
             ?? throw new ArgumentNullException("DigitalOcean:ClusterId config is missing");
        try
        {
            // 1. Get the physical infrastructure ready first
            var connectionModel = await _oceanDbService.CreateTenantDatabaseAsync(clusterId, dbName);
            if (connectionModel == null) throw new Exception("DigitalOcean failed to return connection.");
            // 2. Now create the scope to perform application-level work
            using var scope = _factory.CreateScope();
            // 3. Hydrate the scoped state BEFORE resolving the DB Context
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
            var tenantInfo = scope.ServiceProvider.GetRequiredService<TenantConnectionInfo>();
            var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
            tenantInfo.TenantId = message.TenantId;
            tenantInfo.ConnectionString = connectionModel.ConnectionString;
            tenantProvider.SetTenantId(message.TenantId);
            var payload = CreatePayload(connectionModel, clusterId, dbName, message.TenantId);
            //initial migration
            _migrationService.Migrate(payload.ConnectionString);
            await _publisher.Publish(payload);
            await _publisher.Publish(new TenantSetInitData { ConnectionString = connectionModel.ConnectionString, TenantId = message.TenantId, });
            await _publisher.Publish(new SchemaVersionUpdatePayload
            {
                CurrentVersion = "1.0.0",
                Status = "Active",
                System = "HRIS",
                TenantId = message.TenantId,
            });
            await uow.CommitChangesAsync("", context.CancellationToken);
        }
        catch (DOException ex)
        {
            Log.Logger.Error(ex, "Consumer failed. Starting rollback for Tenant {TenantId}", context.Message.TenantId);
            var deleted = await _oceanDbService.DeleteTenantDatabaseAsync(clusterId, dbName);
            if (!deleted)
            {
                Log.Logger.Fatal("CRITICAL: Rollback failed! Manual cleanup required for DB: {DbName}", dbName);
            }
            throw;
        }
    }
    private ConnectionStringPayload CreatePayload(ConnectionModel model,
        string clusterId,
        string dbName,
        Guid TenantId)
    {
        return new ConnectionStringPayload
        {
            ConnectionString = model.ConnectionString,
            Environment = "Production",
            IsActive = true,
            Module = "hrms",
            ServiceOwner = "hrms",
            SchemaVersion = "1",
            TenantId = TenantId,
            ClusterId = clusterId,
            DatabaseName = dbName,
        };
    }
}
