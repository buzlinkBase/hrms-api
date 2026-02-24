using Hrms.Core.Validations;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;
public class CompanyService : BaseService<Company>
{
    public CompanyService(IUnitOfWorkService uow) : base(uow)
    {
    }

    protected override async Task<EvaluationResult> CreateValidatorAsync(Company model, CancellationToken token)
    {
        var validator = new CompanyValidator().Validate(model);
        if (!validator.IsValid)
        {
            return EvaluationResult.Fail(validator.Errors);
        }
        return await base.CreateValidatorAsync(model, token);
    }
    public async Task AddAsync(Company model, CancellationToken token)
    {
        await CreateAsync(model, token);
        _uow.SaveChanges();
    }
    public async Task UpdateAsync(Company model, CancellationToken token)
    {
        await ModifyAsync(model, token);
    }
    public async Task AddOrUpdateAsync(Company model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
    }
    public async Task<List<Company>> FindAllAsync()
    {
        return await GetQueryable().ToListAsync();
    }
    public async Task<Company?> FineOneAsync(CancellationToken token)
    {
        return await GetQueryable().FirstOrDefaultAsync(token);
    }
    public async Task DeleteAync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
    }
}

