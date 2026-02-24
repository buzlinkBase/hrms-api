using AutoMapper;
using Newtonsoft.Json;

namespace Hrms.Api.Messaging.Message_Handlers;

public class CreateDTRSummaryHandler : IMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    public CreateDTRSummaryHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task Handle(string message)
    {
        try
        {
            var models = JsonConvert.DeserializeObject<List<CreateDailyRecord>>(message);
            if (models == null) return;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<DailyRecordService>();
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
            var dtr = mapper.Map<List<DailyRecord>>(models);
            await new DTRSummaryDelete().DeleteAsync(service, models);
            await service.AddRangeAsync(dtr);
            await service.CommitChangesAsync();
        }
        catch (Exception ex)
        {
        }
    }
}
public class DeleteDTRSummaryHandler : IMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    public DeleteDTRSummaryHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task Handle(string message)
    {
        try
        {
            var models = JsonConvert.DeserializeObject<List<CreateDailyRecord>>(message);
            if (models == null) return;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<DailyRecordService>();
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
            var dtr = mapper.Map<List<DailyRecord>>(models);
            await new DTRSummaryDelete().DeleteAsync(service, models);
            await service.CommitChangesAsync();
        }
        catch (Exception ex)
        {
        }
    }
}
public class DTRSummaryDelete
{
    public async Task DeleteAsync(DailyRecordService service, List<CreateDailyRecord> models)
    {
        if (service == null || models == null || models.Count == 0)
            return;

        // Defensive: ensure WorkDate is not default
        var validModels = models.Where(x => x.WorkDate != default).ToList();
        if (validModels.Count == 0)
            return;

        var fromDate = validModels.Min(x => x.WorkDate);
        var toDate = validModels.Max(x => x.WorkDate);
        var employeeIds = validModels.Select(x => x.EmployeeId).Distinct().ToList();

        var range = new DateRangePayload(fromDate, toDate);
        await service.DeleteAsync(range, employeeIds);
    }
}
