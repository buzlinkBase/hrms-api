using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class TravelOrderApplicationService : BaseService<TravelOrderApplication>
{
    public TravelOrderApplicationService(IUnitOfWorkService uow) : base(uow) { }

    public async Task AddAsync(TravelOrderApplication model, CancellationToken token)
    {
        model.ApplicationDate = DateTime.UtcNow;
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(UpdateTravelOrderApplication payload, CancellationToken token)
    {
        var existing = await Context.TravelOrderApplications.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<TravelOrderApplication>> FindAllAsync(CancellationToken token, DateOnly? from = null, DateOnly? to = null)
    {
        var query = GetQueryable();
        if (from.HasValue) query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) >= from.Value);
        if (to.HasValue) query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) <= to.Value);
        return await query.ToListAsync(token);
    }

    public async Task<Dictionary<TravelKey, List<TravelOrderApplication>>> FindByDateRangeAsync(DateOnly fromDate, DateOnly toDate, HashSet<Guid> employeeIds, CancellationToken token)
    {
        var data = await _uow.Repository
                .Find<TravelOrderApplication>(x => x.StartDate >= fromDate
                    && x.EndDate <= toDate
                    && employeeIds.Contains(x.EmployeeId)
                    && x.ApprovalStatus == ApprovalStatus.Approved
                    )
                 .GroupBy(a => new TravelKey(a.EmployeeId))
                 .ToDictionaryAsync(g => g.Key, g => g.OrderBy(x => x.StartDate).ToList(), token);
        ;
        return data;
    }

    // Self-service "My Official Business Applications" — every status, newest first, scoped to
    // one employee. See MeController.GetMyTravelOrderApplications.
    public Task<List<TravelOrderApplication>> FindAllForEmployeeAsync(Guid employeeId, CancellationToken token)
    {
        return GetQueryable(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(token);
    }

    public async Task<TravelOrderApplication?> FineOneAsync(Guid id, CancellationToken token)
    {
        return await GetOneAsync(id, token);
    }

    public async Task Delete(Guid id, CancellationToken token)
    {
        await RemoveAsync(id, token);
        await CommitChangesAsync(token);
    }
}
public readonly record struct TravelKey(Guid EmpId);
