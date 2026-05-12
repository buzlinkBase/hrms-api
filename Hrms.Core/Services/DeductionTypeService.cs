using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class DeductionTypeService : BaseService<DeductionType>
{
    public DeductionTypeService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(DeductionType model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(DeductionType model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task AddOrUpdateAsync(DeductionType model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task<List<DeductionType>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }
    public async Task<DeductionType?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

