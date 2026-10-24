using Hrms.Core.Services.OrgChart;
using Hrms.Core.Validations;
using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class DepartmentService : BaseService<Department>
{
    public DepartmentService(IUnitOfWorkService service) : base(service)
    {
    }

    protected override async Task<EvaluationResult> CreateValidatorAsync(Department model, CancellationToken token)
    {
        var validator = new DepartmentValidator();
        var result = validator.Validate(model);
        if (!result.IsValid)
        {
            return EvaluationResult.Fail(result.Errors);
        }

        if (await NameIsTakenAsync(x => x.Name, model.Name, model.Id, token))
        {
            return EvaluationResult.Fail($"A department named '{model.Name.Trim()}' already exists.");
        }
        if (model.ParentDepartmentId != null && await WouldCreateCycleAsync(model.Id, model.ParentDepartmentId.Value, token))
        {
            return EvaluationResult.Fail("A department can't sit under itself or one of its own sub-departments.");
        }
        return await base.CreateValidatorAsync(model, token);
    }

    // Walks up from the proposed parent; reaching `id` means the move would close a loop.
    private async Task<bool> WouldCreateCycleAsync(Guid id, Guid parentId, CancellationToken token)
    {
        var parents = await Context.Departments.AsNoTracking()
            .Select(d => new { d.Id, d.ParentDepartmentId })
            .ToDictionaryAsync(d => d.Id, d => d.ParentDepartmentId, token);
        return OrgChartBuilder.ReachesSelf(id, parentId, parents);
    }

    public async Task AddAsync(Department model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdateDepartment payload, CancellationToken token)
    {
        var existing = await Context.Departments.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }
    public async Task AddOrUpdateAsync(Department model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
        await CommitChangesAsync();
    }
    public async Task<List<Department>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }
    public async Task<Department?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

