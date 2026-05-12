
using Hrms.Core.Validations;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class ClientService : BaseService<Client>
{
    public ClientService(IUnitOfWorkService uow) : base(uow)
    {
    }
    protected override async Task<EvaluationResult> CreateValidatorAsync(Client model, CancellationToken token)
    {
        var validator = await new ClientValidator().ValidateAsync(model, token);
        if (!validator.IsValid)
        {
            return EvaluationResult.Fail(validator.Errors);
        }
        return await base.CreateValidatorAsync(model, token);
    }
    public async Task AddAsync(Client model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task AddRangeAsync(List<Client> models, CancellationToken token)
    {
        await CreateRangeAsync(models, token);

    }
    public async Task UpdateAsync(Client model, CancellationToken token)
    {
        await ModifyAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task AddOrUpdateAsync(Client model, CancellationToken token)
    {
        await CreateOrUpdateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task<List<Client>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }
    public async Task<Client?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}

