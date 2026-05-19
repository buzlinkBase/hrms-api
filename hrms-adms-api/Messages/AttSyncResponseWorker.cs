using Hrms.adms.Services;

namespace Hrms.adms.Messages;

public class AttSyncResponseWorker : IConsumer<BatchAttConfirmation> 
{
    private readonly AttendanceService _service;
    public AttSyncResponseWorker(AttendanceService service)
    {
        _service = service;
    }
    public async Task Consume(ConsumeContext<BatchAttConfirmation> context)
    {
        var message = context.Message;
        await _service.UpdateSync(message.BatchId);
        await _service.CommitChangesAsync(context.CancellationToken);
    }
}
