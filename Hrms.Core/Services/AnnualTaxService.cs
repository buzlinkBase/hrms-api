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
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(UpdateAnnualTax payload, CancellationToken token)
    {
        var existing = await Context.GovAnnualTaxes.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
            throw new NotFoundException("Record not found");

        payload.Adapt(existing);
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

    public async Task<List<AnnualTaxTable>> FindAllAsync(DateOnly effectivity, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EffectiveDate == effectivity)
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
