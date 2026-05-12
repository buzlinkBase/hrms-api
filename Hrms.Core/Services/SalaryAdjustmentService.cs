using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class SalaryAdjustmentService : BaseService<SalaryAdjustment>
{
    public SalaryAdjustmentService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(SalaryAdjustment model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task UpdateAsync(SalaryAdjustment model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }
  
    public async Task<List<SalaryAdjustment>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }
    public async Task<SalaryAdjustment?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

