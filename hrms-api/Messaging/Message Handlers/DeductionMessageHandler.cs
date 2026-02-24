using AutoMapper;
using Hrms.Domain.Entities.HR;
using Newtonsoft.Json;

namespace Hrms.Api.Messaging.Message_Handlers;

public class DeductionMessageHandler : IMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    public DeductionMessageHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task Handle(string message)
    {
        try
        {
            var model = JsonConvert.DeserializeObject<UpdateDeduction>(message);
            if (model == null) return;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<DeductionService>();
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
            var entity = mapper.Map<Deduction>(model);
            if (entity == null) return;
            await service.AddOrUpdateAsync(entity);
            await service.CommitChangesAsync();
        }
        catch (Exception ex)
        {
        }
    }
}
