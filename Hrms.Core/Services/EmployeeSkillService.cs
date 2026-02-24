using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

public class EmployeeSkillService : BaseService<Skill>
{
    public EmployeeSkillService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(Skill model,
        CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(Skill model,
        CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<Skill>> FindAllAsync(Guid empId, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EmployeeId == empId)
            .ToListAsync(token);
    }
    public async Task<Skill?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

