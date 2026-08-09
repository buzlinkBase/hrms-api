using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class PositionService : BaseService<Position>
{
    public PositionService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task AddAsync(Position model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task UpdateAsync(UpdatePosition payload, CancellationToken token)
    {
        var existing = await Context.Positions.FindAsync(new object[] { payload.Id }, token);
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }


    public async Task<List<Position>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }

    public async Task<Position?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

