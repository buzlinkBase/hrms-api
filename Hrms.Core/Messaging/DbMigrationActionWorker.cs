using MassTransit;

namespace Hrms.Core.Messaging;

public class DbMigrationActionWorker : IConsumer<MigrateTenantDb>
{
    private readonly TenantConnectionStringInfo _connectionInfo;
    private readonly IPublishEndpoint _publisher;
    private readonly IMigrationService _migrationService;

    public DbMigrationActionWorker(
        TenantConnectionStringInfo connectionInfo,
        IPublishEndpoint publisher,
        IMigrationService migrationService)
    {
        _connectionInfo = connectionInfo;
        _publisher = publisher;
        _migrationService = migrationService;
    }

    public async Task Consume(ConsumeContext<MigrateTenantDb> context)
    {
        var message = context.Message;
        if (message.System != "HRIS") return;
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
