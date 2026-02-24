using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class UnderTimeApplicationService : BaseService<UnderTimeApplication>
{
    public UnderTimeApplicationService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(UnderTimeApplication model, CancellationToken token)
    {
        await CreateAsync(model, token);
    }
    public async Task UpdateAsync(UnderTimeApplication model, CancellationToken token)
    {
        await ModifyAsync(model, token);
    }
    public async Task<List<UnderTimeApplication>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }
    public async Task<UnderTimeApplication?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
    }

    public async Task<Dictionary<UTKey, UnderTimeApplication?>> FindByDateRangeAsync(DateOnly from, DateOnly to,
        HashSet<Guid> employeeIds,
        CancellationToken token)
    {
        Dictionary<UTKey, UnderTimeApplication?> data = await _uow.Repository
                 .Find<UnderTimeApplication>(x => x.OTStatus == ApprovalStatus.Approved &&
                 (x.PayrollDate >= from &&
                 x.PayrollDate <= to) &&
                 employeeIds.Contains(x.EmployeeId))
                 .GroupBy(a => new UTKey(a.EmployeeId, a.PayrollDate))
                 .ToDictionaryAsync(g => g.Key, g => g.OrderBy(x => x.PayrollDate).FirstOrDefault(), token);
        ;
        return data;
    }
}
public readonly record struct UTKey(Guid EmpId, DateOnly OTDate);
