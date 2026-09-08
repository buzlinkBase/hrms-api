using Hrms.Domain.Entities.EmployeeEntities;
using Mapster;

namespace Hrms.Core.Services;

public class EmployeePriorEmployerTaxRecordService : BaseService<PriorEmployerTaxRecord>
{
    public EmployeePriorEmployerTaxRecordService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(PriorEmployerTaxRecord model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }
    public async Task UpdateAsync(UpdatePriorEmployerTaxRecord payload, CancellationToken token)
    {
        var existing = await Context.PriorEmployerTaxRecords.FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }
    public async Task<List<PriorEmployerTaxRecord>> FindAllAsync(Guid empId, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EmployeeId == empId)
            .OrderByDescending(x => x.Year)
            .ToListAsync(token);
    }
    // Consumed by TaxAnnualizationService.ComputeAsync to consolidate every in-scope
    // employee's prior-employer figures for one annualization run in a single query.
    public async Task<List<PriorEmployerTaxRecord>> FindAllByYearAsync(int year, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.Year == year && x.HasPriorEmployer)
            .ToListAsync(token);
    }
    public async Task<PriorEmployerTaxRecord?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);
    }
}
