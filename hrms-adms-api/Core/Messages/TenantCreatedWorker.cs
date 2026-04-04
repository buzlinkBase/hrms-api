namespace Hrms.adms.Core;
public class TenantCreatedWorker : IConsumer<TenantCreatedPayload>
{
    public async Task Consume(ConsumeContext<TenantCreatedPayload> context)
    {
    }
}
