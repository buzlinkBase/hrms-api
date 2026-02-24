using Hrms.Core.Validations;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class BranchService : BaseService<Branch>
{
    private readonly CompanyService _companyService;

    public BranchService(IUnitOfWorkService uow, CompanyService companyService) : base(uow)
    {
        _companyService = companyService;
    }
    protected override async Task<EvaluationResult> CreateValidatorAsync(Branch model, CancellationToken token)
    {
        var result = new BranchValidator(_uow).Validate(model);
        if (!result.IsValid)
        {
            return EvaluationResult.Fail(result.Errors);
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
    }
    public async Task<List<Branch>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }
    public async Task<Branch?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

