
using Elastic.Clients.Elasticsearch.Core.TermVectors;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

public class RestDayDateService : BaseService<RestDayDate>
{
    public RestDayDateService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddOrUpdate(CreateRestDayDate payload, CancellationToken token)
    {

        var existingEntity = await GetQueryable(x =>
                x.EmployeeId == payload.EmployeeId &&
                x.PayrollDate == payload.PayrollDate)
            .FirstOrDefaultAsync(token);

        if (existingEntity != null)
        {
            existingEntity.EmployeeId = payload.EmployeeId;
            existingEntity.PayrollDate = payload.PayrollDate;
            await CreateOrUpdateAsync(existingEntity, token);
        }
        else
        {
            var newModel = new RestDayDate
            {
                EmployeeId = payload.EmployeeId,
                PayrollDate = payload.PayrollDate
            };
            await CreateOrUpdateAsync(newModel, token);
        }

        await CommitChangesAsync(token);
    }

    public async Task<Dictionary<ResDaykey, RestDayDate>> LoadRestDayDate(
        DateOnly fromDate,
        DateOnly toDate,
        HashSet<Guid> employeeIds,
        CancellationToken token)
    {
        // 1. Fetch raw data using anonymous type (Supported by EF)
        var rawData = await GetQueryable(x =>
                employeeIds.Contains(x.EmployeeId) &&
                x.PayrollDate >= fromDate &&
                x.PayrollDate <= toDate)
            .Select(x => new
            {
                x.EmployeeId,
                x.PayrollDate,
                Entity = x
            })
            .ToListAsync(token);

        return rawData.ToDictionary(
            x => new ResDaykey(x.EmployeeId, x.PayrollDate),
            x => x.Entity
        );
    }

}

