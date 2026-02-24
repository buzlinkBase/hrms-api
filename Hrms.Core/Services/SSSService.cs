using AutoMapper;
using AutoMapper.QueryableExtensions;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class SSSService : BaseService<SSSTable>
{
    private readonly IMapper _mapper;

    public SSSService(IUnitOfWorkService uow, IMapper mapper) : base(uow)
    {
        _mapper = mapper;
    }
    public async Task AddAsync(SSSTable model, CancellationToken token)
    {
        model.TotalContibution = model.ER + model.ER + model.EC;
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(SSSTable model, CancellationToken token)
    {
        model.TotalContibution = model.ER + model.ER + model.EC;
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }
  

    public async Task<List<DateOnly>> VersionsAsync(DateOnly effectivity)
    {
        return await GetQueryable()
            .GroupBy(x => x.EffectiveDate)
            .Select(x => x.Key)
            .ToListAsync();
    }

    public async Task<List<SSSTable>> FindAllAsync(DateOnly effectivity,
        CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EffectiveDate == effectivity)
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
            .ProjectTo<SSSModel>(_mapper.ConfigurationProvider)
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

