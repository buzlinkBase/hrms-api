using MassTransit;
namespace OnePunch.Auth.Core.Messaging;

public class TenantUserConfirmed : IConsumer<TenantCreatedPayload>
{
    public TenantUserConfirmed()
    {
    }

    public async Task Consume(ConsumeContext<TenantCreatedPayload> context)
    { 
        //init configs
        //rules/policies
        //default setups
        /// leaves
        /// holidays
        /// rates
        /// etc..
    }
}

public class TenantDeletedWorker : IConsumer<TenantDeletedPayload>
{
    public TenantDeletedWorker()
    {
    }

    public async Task Consume(ConsumeContext<TenantDeletedPayload> context)
    {
        //delete data
    }
}