using Hrms.Core.Interfaces;
using Hrms.Core.Validations;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class BranchService : BaseService<Branch>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IMapper _mapper;

    public BranchService(IUnitOfWorkService uow,
        IMapper mapper) : base(uow)
    {
        _mapper = mapper;
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

    public async Task AddAsync(CreateBranch model, CancellationToken token = default)
    {
        var branch = _mapper.Map<Branch>(model);
        GenerateCode(branch);
        await CreateAsync(branch, token);
    }

 
    public async Task AddOrUpdateAsync(UpdateBranch model, CancellationToken token = default)
    {
        var branch = _mapper.Map<Branch>(model);
        await CreateOrUpdateAsync(branch, token);
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

