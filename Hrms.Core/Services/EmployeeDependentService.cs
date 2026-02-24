using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;
public class EmployeeDependentService : BaseService<Dependent>
{
    public EmployeeDependentService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(Dependent model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task UpdateAsync(Dependent model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task AddOrUpdateAsync(Dependent model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
    }
    public async Task<List<Dependent>> FindAllAsync(Guid empId, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EmployeeId == empId)
            .ToListAsync(token);
    }
    public async Task<Dependent?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

