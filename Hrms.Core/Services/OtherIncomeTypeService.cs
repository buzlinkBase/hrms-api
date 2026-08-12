using Hrms.Domain.Entities;
using Mapster;

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
    public async Task UpdateAsync(UpdateOtherIncomeType payload, CancellationToken token)
    {
        var existing = await Context.AllowanceTypes.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
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

