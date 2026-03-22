using MassTransit;

namespace Hrms.Core.Messaging;

public class DbMigrationActionWorker : IConsumer<MigrateTenantDb>
{
    private readonly TenantConnectionInfo _connectionInfo;
    private readonly IPublishEndpoint _publisher;
    private readonly IMigrationService _migrationService;

    public DbMigrationActionWorker(
        TenantConnectionInfo connectionInfo,
        IPublishEndpoint publisher,
        IDbService digitalOceanDbService,
        IMigrationService migrationService)
    {
        _connectionInfo = connectionInfo;
        _publisher = publisher;
        _migrationService = migrationService;
    }

    public async Task Consume(ConsumeContext<MigrateTenantDb> context)
    {
        var message = context.Message;
        if (message.System != "ADMS") return;
        var connectionString = _connectionInfo.ConnectionString;
        _migrationService.Migrate(connectionString);
        var payload = new SchemaVersionUpdatePayload
        {
            CurrentVersion = message.TargetVersion,
            System = message.System,
            TenantId = message.TenantId,
        };
        await _publisher.Publish(payload);
    }
}
