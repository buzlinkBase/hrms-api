using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class CostCenterService : BaseService<CostCenters>
{
    public CostCenterService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(CostCenters model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdateCostCenter payload, CancellationToken token)
    {
        var existing = await Context.Areas.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);

    }

    public async Task<List<CostCenters>> FindAllAsync()
    {
        return await GetQueryable().ToListAsync();
    }
    public async Task<CostCenters?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

