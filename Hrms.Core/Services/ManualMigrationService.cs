using Hrms.Domain.Entities;
using MassTransit;
namespace Hrms.Core.Services;

public class ManualMigrationService : BaseService<Attendance>
{
    private readonly IPublishEndpoint _publisher;
    public ManualMigrationService(
        IUnitOfWorkService uow,
        IPublishEndpoint publisher) : base(uow)
    {
        _publisher = publisher;
    }
    public async Task Migrate(MigrateTenantDb migrate)
    {
        await _publisher.Publish(migrate);
        await CommitChangesAsync();
    }
}
