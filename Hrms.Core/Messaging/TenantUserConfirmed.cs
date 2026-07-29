using MassTransit;
namespace OnePunch.Auth.Core.Messaging;

public class TenantUserConfirmed : IConsumer<TenantCreationCompleted>
{
    public TenantUserConfirmed()
    {
    }

    public async Task Consume(ConsumeContext<TenantCreationCompleted> context)
    {
        var message = context.Message;
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