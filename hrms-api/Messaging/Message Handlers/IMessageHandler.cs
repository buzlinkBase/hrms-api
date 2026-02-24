namespace Hrms.Api.Messaging.Message_Handlers;

public interface IMessageHandler
{
    Task Handle(string message);
}
