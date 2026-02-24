using AutoMapper;
using Newtonsoft.Json;

namespace Hrms.Api.Messaging.Message_Handlers;

public class TimeShiftMessageHandler : IMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    public TimeShiftMessageHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task Handle(string message)
    {
        try
        {
            var model = JsonConvert.DeserializeObject<CreateTimeShift>(message);
            if (model == null) return;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<TimeShiftService>();
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
            var entity = mapper.Map<TimeShift>(model);
            if (entity == null) return;
            await service.AddOrUpdateAsync(entity);
            await service.CommitChangesAsync();
        }
        catch (Exception ex)
        {
        }
    }
}
