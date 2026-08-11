using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class UnderTimeApplicationService : BaseService<UnderTimeApplication>
{
    public UnderTimeApplicationService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(UnderTimeApplication model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdateUnderTimeApplication payload, CancellationToken token)
    {
        var existing = await Context.UTApplications.FindAsync(new object[] { payload.Id }, token);
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }
    public async Task<List<UnderTimeApplication>> FindAllAsync(CancellationToken token, DateOnly? from = null, DateOnly? to = null)
    {
        var query = GetQueryable();
        if (from.HasValue) query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) >= from.Value);
        if (to.HasValue) query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) <= to.Value);
        return await query.ToListAsync(token);
    }
    public async Task<UnderTimeApplication?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }

    public async Task<Dictionary<UTKey, UnderTimeApplication?>> FindByDateRangeAsync(DateOnly from, DateOnly to,
        HashSet<Guid> employeeIds,
        CancellationToken token)
    {
        Dictionary<UTKey, UnderTimeApplication?> data = await _uow.Repository
                 .Find<UnderTimeApplication>(x => x.ApprovalStatus == ApprovalStatus.Approved &&
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
