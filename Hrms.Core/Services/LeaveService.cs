using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class LeaveService : BaseService<Leave>
{
    public LeaveService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(Leave model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(Leave model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }
 
    public async Task<List<Leave>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }
    public async Task<Leave?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

