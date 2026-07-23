using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class HDMFService : BaseService<HDMFTable>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;

    public HDMFService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper) : base(uow)
    {
        _config = config;
        _mapper = mapper;
    }
    public async Task AddAsync(HDMFTable model, CancellationToken token)
    {
        model.TotalContribution = model.EmployeeShare + model.EmployerShare;
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(HDMFTable model, CancellationToken token)
    {
        model.TotalContribution = model.EmployeeShare + model.EmployerShare;
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task AddOrUpdateRange(List<HDMFTable> models, CancellationToken token)
    {
        foreach (var model in models)
        {
            model.TotalContribution = model.EmployeeShare + model.EmployerShare;
            await CreateOrUpdateAsync(model, token);
        }
        await CommitChangesAsync(token);
    }


    public async Task<List<DateOnly>> Versions(DateOnly effectivity,
        CancellationToken token)
    {
        return await GetQueryable()
            .GroupBy(x => x.EffectiveDate)
            .Select(x => x.Key)
            .ToListAsync(token);
    }

    public async Task<List<HDMFTable>> FindAllAsync(DateOnly effectivity,
        CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EffectiveDate == effectivity)
            .ToListAsync(token);
    }

    public async Task<List<HDMFModel>> LoadForPayrollrunAsync(DateOnly effectivity,
        CancellationToken token)
    {

        var latest = await _uow.Context.GovPHICs
            .Where(x => x.EffectiveDate <= effectivity)
            .OrderByDescending(x => x.EffectiveDate)
            .FirstOrDefaultAsync(token);

        return await GetQueryable()
            .Where(x => latest == null || x.EffectiveDate == latest.EffectiveDate)
            .ProjectToType<HDMFModel>(_config)
            .ToListAsync(token);

    }

    public async Task<HDMFTable?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

