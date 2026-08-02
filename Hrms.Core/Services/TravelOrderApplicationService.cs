using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class TravelOrderApplicationService : BaseService<TravelOrderApplication>
{
    public TravelOrderApplicationService(IUnitOfWorkService uow) : base(uow) { }

    public async Task AddAsync(TravelOrderApplication model, CancellationToken token)
    {
        model.ApplicationDate = DateTime.UtcNow;
        model.Days = ComputeDays(model.StartDate, model.EndDate);
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(TravelOrderApplication model, CancellationToken token)
    {
        model.Days = ComputeDays(model.StartDate, model.EndDate);
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<TravelOrderApplication>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
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

    private static int ComputeDays(DateTime start, DateTime end)
    {
        var days = (end.Date - start.Date).Days + 1;
        return days < 1 ? 1 : days;
    }
}
