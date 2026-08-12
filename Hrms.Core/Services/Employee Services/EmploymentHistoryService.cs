using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;

namespace Hrms.Core.Services;

public class EmploymentHistoryService : BaseService<EmploymentHistory>
{
    public EmploymentHistoryService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(EmploymentHistory model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdateEmploymentHistory payload, CancellationToken token)
    {
        var existing = await Context.Employments.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<EmploymentHistory>> FindAllAsync(Guid empId
        , CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EmployeeId == empId)
            .ToListAsync(token);
    }
    public async Task<EmploymentHistory?> FineOneAsync(Guid Id,
        CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

