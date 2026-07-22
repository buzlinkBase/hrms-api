using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

public class EmployeeEducationService : BaseService<Education>
{
    public EmployeeEducationService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(Education model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task UpdateAsync(Education model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task<List<Education>> FindAllAsync(Guid empId, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EmployeeId == empId)
            .ToListAsync(token);
    }
    public async Task<Education?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

