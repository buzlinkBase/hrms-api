namespace Hrms.adms.Messages;
public class TenantCreatedWorker : IConsumer<TenantCreationRequest>
{
    public async Task Consume(ConsumeContext<TenantCreationRequest> context)
    {
    }
}
