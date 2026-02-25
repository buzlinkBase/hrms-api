
using AutoMapper;
using Elastic.Clients.Elasticsearch.Core.TermVectors;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

public class RestDayDateService : BaseService<RestDayDate>
{
    private readonly IMapper _mapper;

    public RestDayDateService(IUnitOfWorkService uow,
        IMapper mapper) : base(uow)
    {
        _mapper = mapper;
    }

    public async Task<RestDayModel> AddOrUpdate(CreateRestDayDate payload, CancellationToken token)
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
            await SaveChangesAsync(token);
            await CommitChangesAsync(token);
            return _mapper.Map<RestDayModel>(existingEntity);
        }
        else
        {
            var newModel = new RestDayDate
            {
                EmployeeId = payload.EmployeeId,
                PayrollDate = payload.PayrollDate
            };
            await CreateOrUpdateAsync(newModel, token);
            await CommitChangesAsync(token);
            return _mapper.Map<RestDayModel>(newModel);
        }
    }


    public async Task<List<RestDayDateModel>> FindAllAsync(Guid empId, CancellationToken token)
    {
        var data = await GetQueryable(x => x.EmployeeId == empId)
            .ToListAsync(token);
        return _mapper.Map<List<RestDayDateModel>>(data);
    }

    public async Task<RestDayDateModel> FindOneAsync(Guid Id, CancellationToken token)
    {
        var data = await GetOneAsync(Id, token);
        return _mapper.Map<RestDayDateModel>(data);
    }

    public async Task Remove(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
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

