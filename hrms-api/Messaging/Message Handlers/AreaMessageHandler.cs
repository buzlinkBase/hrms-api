using AutoMapper;
using Hrms.Domain.Entities.HR;
using Newtonsoft.Json;

namespace Hrms.Api.Messaging.Message_Handlers;

public class AreaMessageHandler : IMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    public AreaMessageHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task Handle(string message)
    {
        try
        {
            var model = JsonConvert.DeserializeObject<UpdateArea>(message);
            if (model == null) return;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AreaService>();
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
            var entity = mapper.Map<CostCenters>(model);
            if (entity == null) return;
            await service.AddOrUpdateAsync(entity);
            await service.CommitChangesAsync();
        }
        catch (Exception ex)
        {
        }
    }
}
