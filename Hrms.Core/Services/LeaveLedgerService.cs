using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class LeaveLedgerService : BaseService<LeaveLedger>
{
    public LeaveLedgerService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(LeaveLedger model, CancellationToken token)
    {
        if (model == null) return;
        await CreateAsync(model, token);
    }

    public async Task UpdateAsync(LeaveLedger model, CancellationToken token)
    {
        await ModifyAsync(model, token);
    }

    public Task<List<LeaveLedger>> FindAllAsync(CancellationToken token)
    {
        return GetQueryable().ToListAsync(token);
    }

    public async Task<Dictionary<EmployeeLeaveCreditsKey, decimal>> LoadCreditsAsync(List<Guid> employeeIds,
        CancellationToken token)
    {
        return await GetQueryable()
             .Where(x => employeeIds.Contains(x.EmployeeId))
             .GroupBy(x => new EmployeeLeaveCreditsKey(x.EmployeeId, x.LeaveId))
             .ToDictionaryAsync(x => x.Key, x => x.Sum(x => x.Add - x.Less), token)
             ;
    }

    public async Task<LeaveLedger?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
    }
}
