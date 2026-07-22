using Hrms.Core.Validations;
using Hrms.Domain.Entities;

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

        var existing = await _uow.Repository
        .Find<Department>(x => x.Code == model.Code && x.Id != model.Id)
        .FirstOrDefaultAsync(token);

        if (existing != null)
        {
            return EvaluationResult.Fail("Department code already exists.");
        }
        return await base.CreateValidatorAsync(model, token);
    }

    public async Task AddAsync(Department model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(Department model, CancellationToken token)
    {
        await ModifyAsync(model, token);
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

