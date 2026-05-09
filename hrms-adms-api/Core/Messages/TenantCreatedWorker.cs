namespace Hrms.adms.Core;
public class TenantCreatedWorker : IConsumer<TenantCreationRequest>
{
    public async Task Consume(ConsumeContext<TenantCreationRequest> context)
    {
    }
}
