using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class OvertimeApplicationService : BaseService<OverTimeApplication>
{
    public OvertimeApplicationService(IUnitOfWorkService uow) : base(uow) { }
    public async Task AddAsync(OverTimeApplication model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(OverTimeApplication model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task<List<OverTimeApplication>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }
    public async Task<OverTimeApplication?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }

    public async Task<Dictionary<OTKey, OverTimeApplication?>> FindByDateRangeAsync(
      DateOnly from,
      DateOnly to,
      HashSet<Guid> employeeIds,
      CancellationToken token)
    {
        var results = await GetQueryable(x =>
                x.OTStatus == ApprovalStatus.Approved &&
                x.OTDate >= from && x.OTDate <= to &&
                employeeIds.Contains(x.EmployeeId))
            .GroupBy(a => new { a.EmployeeId, a.OTDate }) 
            .Select(g => new
            {
                Key = g.Key,
                Value = g.OrderBy(x => x.OTDate).FirstOrDefault()
            })
            .ToListAsync(token);

        return results.ToDictionary(
            x => new OTKey(x.Key.EmployeeId, x.Key.OTDate),
            x => x.Value
        );
    }
}
public readonly record struct OTKey(Guid EmpId, DateOnly OTDate);
