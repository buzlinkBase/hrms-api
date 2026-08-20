using Hrms.Domain.Entities;
using Mapster;

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
    public async Task UpdateAsync(UpdateSalaryAdjustment payload, CancellationToken token)
    {
        var existing = await Context.Set<SalaryAdjustment>().FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<SalaryAdjustment>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }

    public async Task<Dictionary<EmployeeKey, List<SalaryAdjustment>>> LoadAsync(
        List<Guid> empIds, DateOnly fromDate, DateOnly toDate, CancellationToken token)
    {
        return await GetQueryable(x =>
                empIds.Contains(x.EmployeeId) &&
                x.PayrollDate >= fromDate &&
                x.PayrollDate <= toDate)
            .GroupBy(x => x.EmployeeId)
            .ToDictionaryAsync(g => new EmployeeKey(g.Key), g => g.ToList(), token);
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

