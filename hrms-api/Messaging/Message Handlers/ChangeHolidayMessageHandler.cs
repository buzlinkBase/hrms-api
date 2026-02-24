using AutoMapper;
using Newtonsoft.Json;

namespace Hrms.Api.Messaging.Message_Handlers;

public class ChangeHolidayMessageHandler : IMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    public ChangeHolidayMessageHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task Handle(string message)
    {
        try
        {
            var model = JsonConvert.DeserializeObject<CreateChangeHoliday>(message);
            if (model == null) return;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ChangeHolidayService>();
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
            //var entity = mapper.Map<ChangeHoliday>(model);
            //if (entity == null) return;
            await service.AddAsync(model);
            await service.CommitChangesAsync();
        }
        catch (Exception ex)
        {
        }
    }
}
