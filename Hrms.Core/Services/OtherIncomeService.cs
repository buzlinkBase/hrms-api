using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class OtherIncomeService : BaseService<OtherIncome>
{
    public OtherIncomeService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(OtherIncome model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task UpdateAsync(OtherIncome model, CancellationToken token)
    {
        var oldRecord = await GetOneAsync(model.Id, token);
        await ModifyAsync(model, token);
        await UpdateIsTaxableAsync(oldRecord!, model, token);
        await CommitChangesAsync(token);
    }

    private async Task UpdateIsTaxableAsync(OtherIncome oldRecord,
        OtherIncome newRecord,
        CancellationToken token)
    {
        if (oldRecord != null && oldRecord!.IsTaxable != newRecord.IsTaxable)
        {
            await _uow.Context.OtherIncomeApplications
             .Where(x => x.IncomeId == newRecord.Id)
             .ExecuteUpdateAsync(setters => setters
             .SetProperty(x => x.IsTaxable, newRecord.IsTaxable)
             , token);
        }
    } 
    public async Task<List<OtherIncome>> FindAllAsync()
    {
        return await GetQueryable().ToListAsync();
    }

    public async Task<List<OtherIncome>> LoadOtherIncomeAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }

    public async Task<OtherIncome?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task DeleteAsync(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

