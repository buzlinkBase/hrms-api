using Hrms.adms.Core.Services;

namespace Hrms.Core.Messaging;

public class CreateDeviceCommandWorker : IConsumer<DeviceCommandWrapper> 
{
    private readonly DeviceService _service;
    public CreateDeviceCommandWorker(DeviceService service)
    { 
        _service = service;
    }
    public async Task Consume(ConsumeContext<DeviceCommandWrapper> context)
    {

        var message = context.Message.Commands;
        await _service.CreateCommand(message);
    }
}
