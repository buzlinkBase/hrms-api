using EvolveDb.Migration;
using Hrms.Infrastructure.Services;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Core.Messaging;

public class TenantCreatedWorker : IConsumer<TenantCreatedPayload>
{
    private readonly IPublishEndpoint _publisher;
    private readonly IDigitalOceanDbService _oceanDbService;
    private readonly IMigrationService _migrationService;
    private readonly IServiceScopeFactory _factory;

    public TenantCreatedWorker(
        IPublishEndpoint publisher,
        IDigitalOceanDbService digitalOceanDbService,
        IMigrationService  migrationService,
        IServiceScopeFactory factory)
    {
        _publisher = publisher;
        _oceanDbService = digitalOceanDbService;
        _migrationService = migrationService;
        _factory = factory;
    }

    public async Task Consume(ConsumeContext<TenantCreatedPayload> context)
    {
        var message = context.Message;
        string dbName = $"hrms_{message.TenantId:N}";
        try
        {
            // 1. Get the physical infrastructure ready first
            var connectionModel = await _oceanDbService.CreateTenantDatabaseAsync(dbName);
            if (connectionModel == null) throw new Exception("DigitalOcean failed to return connection.");
            // 2. Now create the scope to perform application-level work
            using var scope = _factory.CreateScope();
            // 3. Hydrate the scoped state BEFORE resolving the DB Context
            var connectionInfo = scope.ServiceProvider.GetRequiredService<TenantConnectionInfo>();
            var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
            connectionInfo.TenantId = message.TenantId;
            connectionInfo.ConnectionString = connectionModel.ConnectionString;
            tenantProvider.SetTenantId(message.TenantId);
            // 4. NOW it is safe to resolve the Unit of Work
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkService>();
            // 5. Logic & Publish
            var payload = new ConnectionStringPayload
            {
                ConnetionString = connectionModel.ConnectionString,
                RawConnection = connectionModel.RawConnectionString,
                Environment = "Production",
                IsActive = true,
                Module = "hrms",
                ServiceOwner = "hrms",
                SchemaVersion = "1",
                TenantId = message.TenantId,
            };
            await _publisher.Publish(payload);
            _migrationService.Migrate(payload.ConnetionString);
            await uow.CommitChangesAsync("", context.CancellationToken);
        }
        catch (DOException ex)
        {
            Log.Logger.Error(ex, "Consumer failed. Starting rollback for Tenant {TenantId}", context.Message.TenantId);
            var deleted = await _oceanDbService.DeleteTenantDatabaseAsync(dbName);
            if (!deleted)
            {
                // This is a big deal - DB exists but couldn't be deleted
                Log.Logger.Fatal("CRITICAL: Rollback failed! Manual cleanup required for DB: {DbName}", dbName);
            }
            throw;
        }
    }
}
