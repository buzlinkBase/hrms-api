using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class SectionService : BaseService<Section>
{

    public SectionService(IUnitOfWorkService service) : base(service)
    {
    }

    // Sections belong to a department, so a name only has to be unique within that department.
    protected override async Task<EvaluationResult> CreateValidatorAsync(Section model, CancellationToken token)
    {
        var departmentId = model.DepartmentId;
        if (await NameIsTakenAsync(x => x.Name, model.Name, model.Id, token, x => x.DepartmentId == departmentId))
        {
            return EvaluationResult.Fail($"This department already has a section named '{model.Name.Trim()}'.");
        }
        return await base.CreateValidatorAsync(model, token);
    }
    public async Task AddAsync(Section model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task UpdateAsync(UpdateSection payload, CancellationToken token)
    {
        var existing = await Context.Sections.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
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
        return await GetOneReadOnlyAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

