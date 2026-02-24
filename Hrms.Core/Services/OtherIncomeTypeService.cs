using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class OtherIncomeTypeService : BaseService<OtherIncomeType>
{
    public OtherIncomeTypeService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(OtherIncomeType model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(OtherIncomeType model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task<List<OtherIncomeType>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }
    public async Task<OtherIncomeType?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

