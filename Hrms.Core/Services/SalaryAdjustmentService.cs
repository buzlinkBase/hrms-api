using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class SalaryAdjustmentService : BaseService<SalaryAdjustment>
{
    public SalaryAdjustmentService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddAsync(SalaryAdjustment model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);

    }
    public async Task UpdateAsync(UpdateSalaryAdjustment payload, CancellationToken token)
    {
        var existing = await Context.Set<SalaryAdjustment>().FindAsync(new object[] { payload.Id }, token);
        if (existing == null)
        {
            throw new NotFoundException("Record not found");
        }
        payload.Adapt(existing);
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }

    public async Task<List<SalaryAdjustment>> FindAllAsync(CancellationToken token)
    {
        return await GetQueryable().ToListAsync(token);
    }

    // ConsumedByPayrollId == null is the real gatekeeper now — a row already stamped by a
    // saved payroll run (regular or Last Pay) never matches again, regardless of date range,
    // closing the double-application gap the plain date-range match used to allow.
    public async Task<Dictionary<EmployeeKey, List<SalaryAdjustment>>> LoadAsync(
        List<Guid> empIds, DateOnly fromDate, DateOnly toDate, CancellationToken token)
    {
        return await GetQueryable(x =>
                empIds.Contains(x.EmployeeId) &&
                x.PayrollDate >= fromDate &&
                x.PayrollDate <= toDate &&
                x.ConsumedByPayrollId == null)
            .GroupBy(x => x.EmployeeId)
            .ToDictionaryAsync(g => new EmployeeKey(g.Key), g => g.ToList(), token);
    }

    // Loads the specific rows HR confirmed in the Last Pay review step — re-checks
    // ConsumedByPayrollId == null so a row consumed by something else between the review
    // screen loading and Generate being clicked is silently skipped rather than re-applied.
    public async Task<List<SalaryAdjustment>> FindByIdsAsync(List<Guid> ids, CancellationToken token)
    {
        return await GetQueryable(x => ids.Contains(x.Id) && x.ConsumedByPayrollId == null)
            .ToListAsync(token);
    }

    // For Last Pay's review step — every not-yet-consumed adjustment for these employees,
    // regardless of date, so HR can see (and explicitly confirm) exactly what's pending before
    // any of it is applied. See LastPayrollService.GetAvailableSalaryAdjustmentsAsync.
    public async Task<List<SalaryAdjustment>> FindAvailableAsync(List<Guid> empIds, CancellationToken token)
    {
        return await GetQueryable(x =>
                empIds.Contains(x.EmployeeId) &&
                x.ConsumedByPayrollId == null)
            .OrderBy(x => x.PayrollDate)
            .ToListAsync(token);
    }
    public async Task<SalaryAdjustment?> FineOneAsync(Guid Id, CancellationToken token)
    {
        return await GetOneAsync(Id, token);
    }
    public async Task Delete(Guid Id, CancellationToken token)
    {
        await RemoveAsync(Id, token);
        await CommitChangesAsync(token);

    }
}

