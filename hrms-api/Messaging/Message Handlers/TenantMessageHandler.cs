//using AutoMapper;
//using Newtonsoft.Json;

//namespace Hrms.Api.Messaging.Message_Handlers;

//public class TenantMessageHandler : IMessageHandler
//{
//    private readonly IServiceScopeFactory _scopeFactory;
//    public TenantMessageHandler(IServiceScopeFactory scopeFactory)
//    {
//        _scopeFactory = scopeFactory;
//    }

//    public async Task Handle(string message)
//    {
//        try
//        {
//            var model = JsonConvert.DeserializeObject<UpdateTenant>(message);
//            if (model == null) return;
//            using var scope = _scopeFactory.CreateScope();
//            var service = scope.ServiceProvider.GetRequiredService<TenantService>();
//            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
//            var entity = mapper.Map<Tenant>(model);
//            if (entity == null) return;
//            await service.AddOrUpdateAsync(entity);
//        }
//        catch (Exception ex)
//        {
//        }
//    }

//}
