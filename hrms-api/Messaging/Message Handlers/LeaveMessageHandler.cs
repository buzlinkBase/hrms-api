using AutoMapper;
using Hrms.Domain.Entities.HR;
using Newtonsoft.Json;

namespace Hrms.Api.Messaging.Message_Handlers;

public class LeaveMessageHandler : IMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    public LeaveMessageHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task Handle(string message)
    {
        try
        {
            var model = JsonConvert.DeserializeObject<UpdateLeave>(message);
            if (model == null) return;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<LeaveService>();
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
            var entity = mapper.Map<Leave>(model);
            if (entity == null) return;
            await service.AddOrUpdateAsync(entity);
            await service.CommitChangesAsync();
        }
        catch (Exception ex)
        {
        }
    }
}
