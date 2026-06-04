namespace Hrms.adms.Messages;

public class DbMigrationActionWorker(IConnectionClient connectionClient, IMigrationService migrationService, IPublishEndpoint publisher) : IConsumer<MigrateTenantDb>
{
    public async Task Consume(ConsumeContext<MigrateTenantDb> context)
    {
        var msg = context.Message;
        var response = await connectionClient.FindConnectionAsync(msg.TenantId, "adms");

        if (response?.Data is null || !response.Data.Success)
        {
            Log.Warning("DbMigrationActionWorker: no ADMS connection string for tenant {TenantId}", msg.TenantId);
            await publisher.Publish(new SchemaVersionUpdatePayload
            {
                TenantId = msg.TenantId,
                System = msg.System,
                CurrentVersion = msg.CurrentVersion,
                Status = "Failed"
            });
            return;
        }

        migrationService.Migrate(response.Data.ConnectionString);
        Log.Information("DbMigrationActionWorker: migrations applied for tenant {TenantId}", msg.TenantId);

        await publisher.Publish(new SchemaVersionUpdatePayload
        {
            TenantId = msg.TenantId,
            System = msg.System,
            CurrentVersion = msg.TargetVersion,
            Status = "Success"
        });
    }
}
