using Hrms.Domain.Entities;
using Mapster;

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

    public async Task UpdateAsync(UpdateOtherIncome payload, CancellationToken token)
    {
        var existing = await Context.Allowances.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        var wasTaxable = existing.IsTaxable;
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await UpdateIsTaxableAsync(wasTaxable, existing, token);
        await CommitChangesAsync(token);
    }

    private async Task UpdateIsTaxableAsync(bool wasTaxable,
        OtherIncome newRecord,
        CancellationToken token)
    {
        if (wasTaxable != newRecord.IsTaxable)
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

