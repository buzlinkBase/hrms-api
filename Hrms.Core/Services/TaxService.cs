
using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class TaxService : BaseService<TaxTable>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;

    public TaxService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper) : base(uow)
    {
        _config = config;
        _mapper = mapper;
    }
    public async Task AddAsync(TaxTable model, CancellationToken token)
    {
        model.PercentageInAmountOf = model.RangeFrom;
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdateWax payload, CancellationToken token)
    {
        var existing = await Context.GovTaxes.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        if (existing != null)
        {
            existing.PercentageInAmountOf = existing.RangeFrom;
        }
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<DateOnly>> VersionsAsync(DateOnly effectivity, CancellationToken token)
    {
        return await GetQueryable()
            .GroupBy(x => x.EffectiveDate)
            .Select(x => x.Key)
            .ToListAsync(token);
    }

    public async Task<List<TaxTable>> FindAllAsync(DateOnly effectivity, string payrollType, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EffectiveDate == effectivity && x.PayrollType == payrollType)
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
            .ProjectToType<WTaxModel>(_config)
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

