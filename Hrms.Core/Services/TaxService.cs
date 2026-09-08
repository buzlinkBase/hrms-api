
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
        // Effectivity dating/versioning is not in use — every new bracket defaults to the
        // minimum date so it always applies, regardless of what the client sends.
        model.EffectiveDate = DateOnly.MinValue;
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
        var effectiveDate = existing.EffectiveDate;
        payload.Adapt(existing);
        existing.EffectiveDate = effectiveDate; // not editable via the UI anymore — preserve it
        existing.PercentageInAmountOf = existing.RangeFrom;
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<TaxTable>> FindAllAsync(string payrollType, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.PayrollType == payrollType)
            .OrderBy(x => x.RangeFrom)
            .ToListAsync(token);
    }

    public async Task<List<WTaxModel>> LoadForPayrollrunAsync(CancellationToken token)
    {
        return await GetQueryable()
            .OrderBy(x => x.RangeFrom)
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

