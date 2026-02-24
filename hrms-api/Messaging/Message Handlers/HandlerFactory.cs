namespace Hrms.Api.Messaging.Message_Handlers;

public interface IMessageHandlerFactory
{
    IMessageHandler Create(string routingKey);
}

public class MessageHandlerFactory : IMessageHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;
    public MessageHandlerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IMessageHandler? Create(string routingKey)
    {
        var handler = _serviceProvider.GetKeyedService<IMessageHandler>(routingKey);
        return handler ?? throw new InvalidOperationException($"No IMessageHandler registered for key '{routingKey}'.");
    }
}
