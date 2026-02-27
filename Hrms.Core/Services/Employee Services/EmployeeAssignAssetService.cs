using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

public class EmployeeAssignAssetService : BaseService<AssignAsset>
{
    public EmployeeAssignAssetService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(AssignAsset model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task UpdateAsync(AssignAsset model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task AddOrUpdateAsync(AssignAsset model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
    }
    public async Task<List<AssignAsset>> FindAllAsync(Guid empId, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EmployeeId == empId)
            .ToListAsync(token);
    }
    public async Task<AssignAsset?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

