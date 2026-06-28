namespace Hrms.adms.Messages;

public class TenantCreatedWorker(IConnectionClient connectionClient, IMigrationService migrationService) : IConsumer<TenantCreationRequest>
{
    public async Task Consume(ConsumeContext<TenantCreationRequest> context)
    {
        //var msg = context.Message;
        //var response = await connectionClient.FindConnectionAsync(msg.TenantId, "adms");

        //if (response?.Data is null || !response.Data.Success)
        //{
        //    Log.Warning("TenantCreatedWorker: no ADMS connection string for tenant {TenantId}", msg.TenantId);
        //    return;
        //}

        //migrationService.Migrate(response.Data.ConnectionString);
        //Log.Information("TenantCreatedWorker: migrations applied for tenant {TenantId}", msg.TenantId);

    }
}
