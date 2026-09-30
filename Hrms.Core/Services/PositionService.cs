using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class PositionService : BaseService<Position>
{
    public PositionService(IUnitOfWorkService uow) : base(uow)
    {
    }

    protected override async Task<EvaluationResult> CreateValidatorAsync(Position model, CancellationToken token)
    {
        if (await NameIsTakenAsync(x => x.Name, model.Name, model.Id, token))
        {
            return EvaluationResult.Fail($"A position named '{model.Name.Trim()}' already exists.");
        }
        return await base.CreateValidatorAsync(model, token);
    }

    public async Task AddAsync(Position model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task UpdateAsync(UpdatePosition payload, CancellationToken token)
    {
        var existing = await Context.Positions.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
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
        return await GetOneReadOnlyAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

