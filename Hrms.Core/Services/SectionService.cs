using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class SectionService : BaseService<Section>
{

    public SectionService(IUnitOfWorkService service) : base(service)
    {
    }
    public async Task AddAsync(Section model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await  CommitChangesAsync(token);

    }
    public async Task UpdateAsync(Section model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);

    }
  
    public async Task<List<Section>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable()
            .Include(x => x.Department)
            .ToListAsync(token);
    }

    public async Task<List<Section>> FindByDepartmentsAsync(Guid deptId, CancellationToken token)
    {
        return await GetQueryable()
             .Include(x => x.Department)
             .Where(x => x.DepartmentId == deptId)
             .ToListAsync(token);
    }

    public async Task<Section?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

