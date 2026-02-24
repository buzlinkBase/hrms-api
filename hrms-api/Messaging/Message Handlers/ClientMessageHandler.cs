using AutoMapper;
using Hrms.Domain.Entities.HR;
using Newtonsoft.Json;

namespace Hrms.Api.Messaging.Message_Handlers;

public class ClientMessageHandler : IMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    public ClientMessageHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task Handle(string message)
    {
        try
        {
            var model = JsonConvert.DeserializeObject<UpdateClient>(message);
            if (model == null) return;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ClientService>();
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
            var entity = mapper.Map<Client>(model);
            if (entity == null) return;
            await service.AddOrUpdateAsync(entity);
            await service.CommitChangesAsync();
        }
        catch (Exception ex)
        {
        }
    }
}
