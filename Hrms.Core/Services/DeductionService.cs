using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class DeductionService : BaseService<Deduction>
{
    public DeductionService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(Deduction model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdateDeduction payload, CancellationToken token)
    {
        var existing = await Context.Deductions.FindAsync(new object[] { payload.Id }, token);
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);

    }
    public async Task AddOrUpdateAsync(Deduction model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
    }
    public async Task<List<Deduction>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }
    public async Task<Deduction?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

