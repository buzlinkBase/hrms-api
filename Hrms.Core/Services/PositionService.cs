using Hrms.Domain.Entities;

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
    public async Task UpdateAsync(Position model, CancellationToken token)
    {
        await ModifyAsync(model, token);
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

