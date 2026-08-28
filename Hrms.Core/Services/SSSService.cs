
using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class SSSService : BaseService<SSSTable>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;

    public SSSService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper) : base(uow)
    {
        _config = config;
        _mapper = mapper;
    }
    public async Task AddAsync(SSSTable model, CancellationToken token)
    {
        model.TotalContibution = model.EE + model.ER + model.EC;
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdateSSS payload, CancellationToken token)
    {
        var existing = await Context.GovSSSes.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        if (existing != null)
        {
            existing.TotalContibution = existing.EE + existing.ER + existing.EC;
        }
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }


    public async Task<List<DateOnly>> VersionsAsync(CancellationToken token)
    {
        return await GetQueryable()
            .GroupBy(x => x.EffectiveDate)
            .Select(x => x.Key)
            .OrderByDescending(x => x)
            .ToListAsync(token);
    }

    public async Task<List<SSSTable>> FindAllAsync(DateOnly effectivity,
        CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EffectiveDate == effectivity)
            .OrderBy(x=>x.RangeFrom)
            .ToListAsync(token);
    }

    public async Task<List<SSSModel>> LoadForPayrollrunAsync(DateOnly effectivity,
        CancellationToken token)
    {
        //find applicable version
        var latest = await _uow.Context.GovSSSes
            .Where(x => x.EffectiveDate <= effectivity)
            .OrderByDescending(x => x.EffectiveDate)
            .FirstOrDefaultAsync(token);

        return await GetQueryable()
            .Where(x => latest == null || x.EffectiveDate == latest.EffectiveDate)
            .ProjectToType<SSSModel>(_config)
            .ToListAsync(token);
    }


    public async Task<SSSTable?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

