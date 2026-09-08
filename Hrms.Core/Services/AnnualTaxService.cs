using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class AnnualTaxService : BaseService<AnnualTaxTable>
{
    private readonly TypeAdapterConfig _config;
    private readonly IMapper _mapper;

    public AnnualTaxService(IUnitOfWorkService uow,
        TypeAdapterConfig config,
        IMapper mapper) : base(uow)
    {
        _config = config;
        _mapper = mapper;
    }

    public async Task AddAsync(AnnualTaxTable model, CancellationToken token)
    {
        // Effectivity dating/versioning is not in use — every new bracket defaults to the
        // minimum date so it always applies, regardless of what the client sends.
        model.EffectiveDate = DateOnly.MinValue;
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(UpdateAnnualTax payload, CancellationToken token)
    {
        var existing = await Context.GovAnnualTaxes.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
            throw new NotFoundException("Record not found");

        var effectiveDate = existing.EffectiveDate;
        payload.Adapt(existing);
        existing.EffectiveDate = effectiveDate; // not editable via the UI anymore — preserve it
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<AnnualTaxTable>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable()
            .OrderBy(x => x.RangeFrom)
            .ToListAsync(token);
    }

    public async Task<AnnualTaxTable?> FineOneAsync(Guid id, CancellationToken token)
    {
        return await GetOneAsync(id, token);
    }

    public async Task Delete(Guid id, CancellationToken token)
    {
        await RemoveAsync(id, token);
        await CommitChangesAsync(token);
    }
}
