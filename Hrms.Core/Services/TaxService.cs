
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class TaxService : BaseService<TaxTable>
{
    private readonly IMapper _mapper;

    public TaxService(IUnitOfWorkService uow, IMapper mapper) : base(uow)
    {
        _mapper = mapper;
    }
    public async Task AddAsync(TaxTable model, CancellationToken token)
    {
        model.PercentageInAmountOf = model.RangeFrom;
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(TaxTable model, CancellationToken token)
    {
        model.PercentageInAmountOf = model.RangeFrom;
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<DateOnly>> VersionsAsync(DateOnly effectivity, CancellationToken token)
    {
        return await GetQueryable()
            .GroupBy(x => x.EffectiveDate)
            .Select(x => x.Key)
            .ToListAsync(token);
    }

    public async Task<List<TaxTable>> FindAllAsync(DateOnly effectivity, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EffectiveDate == effectivity)
            .ToListAsync(token);
    }

    public async Task<List<WTaxModel>> LoadForPayrollrunAsync(DateOnly effectivity, CancellationToken token)
    {
        var latest = await _uow.Context.GovTaxes
            .Where(x => x.EffectiveDate <= effectivity)
            .OrderByDescending(x => x.EffectiveDate)
            .FirstOrDefaultAsync(token);

        return await GetQueryable()
            .Where(x => latest == null || x.EffectiveDate == latest.EffectiveDate)
            .ProjectTo<WTaxModel>(_mapper.ConfigurationProvider)
            .ToListAsync(token);
    }

    public async Task<TaxTable?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

