using AutoMapper;
using Hrms.Domain.Entities.HR;
using Newtonsoft.Json;

namespace Hrms.Api.Messaging.Message_Handlers;

public class EmployeeMessageHandler : IMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    public EmployeeMessageHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task Handle(string message)
    {
        try
        {
            var model = JsonConvert.DeserializeObject<UpdateEmployee>(message);
            if (model == null) return;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<EmployeeService>();
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
            var entity = mapper.Map<Employee>(model);
            if (entity == null) return;
            await service.AddOrUpdateAsync(entity);
            await service.CommitChangesAsync();
        }
        catch (Exception ex)
        {
        }
    }
}
