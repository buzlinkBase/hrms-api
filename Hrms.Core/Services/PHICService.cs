
using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class PHICService : BaseService<PHICTable>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;

    public PHICService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper) : base(uow)
    {
        _config = config;
        _mapper = mapper;
    }
    public async Task AddAsync(PHICTable model, CancellationToken token)
    {
        model.TotalContribution = model.EmployeeShare + model.EmployerShare;
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdatePHIC payload, CancellationToken token)
    {
        var existing = await Context.GovPHICs.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        if (existing != null)
        {
            existing.TotalContribution = existing.EmployeeShare + existing.EmployerShare;
        }
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }


    public async Task<List<DateOnly>> Versions(DateOnly effectivity)
    {
        return await GetQueryable()
            .GroupBy(x => x.EffectiveDate)
            .Select(x => x.Key)
            .ToListAsync();
    }

    public async Task<List<PHICTable>> FindAllAsync(DateOnly effectivity, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EffectiveDate == effectivity)
            .ToListAsync(token);
    }

    public async Task<List<PHICModel>> LoadForPayrollrunAsync(DateOnly effectivity, CancellationToken token)
    {
        var latest = await _uow.Context.GovPHICs
            .Where(x => x.EffectiveDate <= effectivity)
            .OrderByDescending(x => x.EffectiveDate)
            .FirstOrDefaultAsync(token);

        return await GetQueryable()
            .Where(x => latest == null || x.EffectiveDate == latest.EffectiveDate)
            .ProjectToType<PHICModel>(_config)
            .ToListAsync(token);

    }


    public async Task<PHICTable?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
    public async Task DeleteAllAsync(CancellationToken token)
    {
        await RemoveAllAsync();
        await CommitChangesAsync(token);
    }
}

