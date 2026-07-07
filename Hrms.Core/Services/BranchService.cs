using Hrms.Core.Validations;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class BranchService : BaseService<Branch>
{
    public BranchService(IUnitOfWorkService service ) : base(service)
    {
    }

    protected override async Task<EvaluationResult> CreateValidatorAsync(Branch model, CancellationToken token)
    {
        var validator = new BranchValidator();
        var result = validator.Validate(model);
        if (!result.IsValid)
        {
            return EvaluationResult.Fail(result.Errors);
        }

        var existing = await _uow.Repository
        .Find<Branch>(x => x.Code == model.Code && x.Id != model.Id)
        .FirstOrDefaultAsync(token);

        if (existing != null)
        {
            return EvaluationResult.Fail("Branch code already exists.");
        }

        return await base.CreateValidatorAsync(model, token);
    }

    public async Task AddAsync(Branch model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(Branch model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task AddOrUpdateAsync(Branch model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
        await CommitChangesAsync();
    }
    public async Task<List<Branch>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }
    public async Task<Branch?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<(Guid Id, string Code)>> GetByCodes(HashSet<string> codes, CancellationToken token = default)
    {
        if (codes == null || !codes.Any())
            return new List<(Guid Id, string Code)>();
        var data = await GetQueryable(x => codes.Contains(x.Code))
            .Select(x => new { x.Id, x.Code })
            .ToListAsync(token);
        return data.Select(x => (x.Id, x.Code)).ToList();
    }
}

