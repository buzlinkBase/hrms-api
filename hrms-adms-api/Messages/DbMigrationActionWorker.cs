namespace Hrms.adms.Messages;
public class DbMigrationActionWorker : IConsumer<MigrateTenantDb>
{
    public async Task Consume(ConsumeContext<MigrateTenantDb> context)
    {
    }
}
