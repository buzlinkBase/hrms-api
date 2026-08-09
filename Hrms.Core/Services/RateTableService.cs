using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class RateTableService : BaseService<RateTable>
{
    public RateTableService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task AddAsync(RateTable model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdateRateTable payload, CancellationToken token)
    {
        var existing = await Context.PremiumRates.FindAsync(new object[] { payload.Id }, token);
        payload.Adapt(existing);
        existing.Description = payload.Type.ToString();
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }
    public Task<List<RateTable>> FindAllAsync(CancellationToken token)
    {
        return GetQueryable()
            .ToListAsync(token);
    }
    public async Task<RateTable?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

