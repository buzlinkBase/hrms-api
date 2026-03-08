using Hrms.Core.Interfaces;
using Hrms.Core.Validations;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class BranchService : BaseService<Branch>
{
    private readonly IBranchClient _branchClient;
    private readonly ITenantProvider _tenantProvider;
    private readonly CompanyService _companyService;
    public BranchService(IUnitOfWorkService uow,
        IBranchClient branchClient,
        ITenantProvider tenantProvider,
        CompanyService companyService) : base(uow)
    {
        _branchClient = branchClient;
        _tenantProvider = tenantProvider;
        _companyService = companyService;
    }

    //protected override async Task<EvaluationResult> CreateValidatorAsync(Branch model, CancellationToken token)
    //{
    //    var result = new BranchValidator(_uow, _tenantProvider).Validate(model);
    //    if (!result.IsValid)
    //    {
    //        return EvaluationResult.Fail(result.Errors);
    //    }
    //    return await base.CreateValidatorAsync(model, token);
    //}

    private void GenerateCode(Branch model)
    {
        var codeCount = GetQueryable().Count();
        if (model != null && string.IsNullOrEmpty(model.Code))
        {
            model.Code = codeCount.FormatCode();
        }
    }

    public async Task AddAsync(Branch model, CancellationToken token = default)
    {
        GenerateCode(model);
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(Branch model, CancellationToken token = default)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task AddOrUpdateAsync(Branch model, CancellationToken token = default)
    {
        await CreateOrUpdateAsync(model, token);
    }
    public async Task<List<Branch>> FindAllAsync(Guid tenantId, CancellationToken token = default)
    {
        return await GetQueryable().ToListAsync(token);
    }

    public async Task<Branch?> FindOneAsync(Guid Id, CancellationToken token = default)
    {
        return await GetOneAsync(Id, token);
    }

    public async Task DeleteAsync(Guid Id, CancellationToken token = default)
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

