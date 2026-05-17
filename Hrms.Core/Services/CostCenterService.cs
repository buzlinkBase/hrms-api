using Hrms.Domain.Entities;

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
    public async Task UpdateAsync(CostCenters model, CancellationToken token)
    {
        await ModifyAsync(model, token);
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

