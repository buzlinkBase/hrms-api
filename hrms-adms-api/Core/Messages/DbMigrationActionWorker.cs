namespace Hrms.Core.Messaging;
public class DbMigrationActionWorker : IConsumer<MigrateTenantDb>
{
    public async Task Consume(ConsumeContext<MigrateTenantDb> context)
    {
    }
}
