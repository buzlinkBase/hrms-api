using MassTransit;
namespace Hrms.Core.Services;
public class ManualMigrationService  
{
    private readonly IUnitOfWorkService _uow;
    private readonly IPublishEndpoint _publisher; 
    public ManualMigrationService(
        IUnitOfWorkService uow,
        IPublishEndpoint publisher )
    {
        _uow = uow;
        _publisher = publisher;
    }
    public async Task Migrate(MigrateTenantDb migrate)
    {
        await _publisher.Publish(migrate);
        _uow.CommitChanges("");
    }
}
