using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// Guards the "Data Immutability" rule: once a calendar year's Year-End Tax Adjustment is
// posted, that year's payroll data locks — new Regular/13th-Month/Last-Pay/Year-End-Adjustment
// generation and draft deletion for that year are blocked until an admin explicitly reopens it.
// See the guard clauses in PayrollProcessorService/ThirteenthMonthPayrollService/
// LastPayrollService/TaxAnnualizationService's GenerateAsync methods and
// PayrollBatchLifecycleService.
public class YearLockService : BaseService<YearLock>
{
    public YearLockService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<bool> IsYearLockedAsync(int year, CancellationToken token)
    {
        var lockRow = await GetQueryable(x => x.Year == year).FirstOrDefaultAsync(token);
        return lockRow?.IsLocked ?? false;
    }

    public async Task<List<YearLock>> GetAllAsync(CancellationToken token)
    {
        return await GetQueryable().OrderByDescending(x => x.Year).ToListAsync(token);
    }

    // Auto-invoked by PayrollBatchLifecycleService.PostBatchAsync when a YearEndAdjustment
    // batch is posted — idempotent (re-locking an already-locked year is a no-op beyond
    // refreshing LockedAt).
    public async Task LockYearAsync(int year, CancellationToken token)
    {
        var existing = await GetQueryable(x => x.Year == year).FirstOrDefaultAsync(token);
        if (existing == null)
        {
            await CreateAsync(new YearLock { Year = year, IsLocked = true, LockedAt = DateTime.UtcNow }, token);
        }
        else
        {
            existing.IsLocked = true;
            existing.LockedAt = DateTime.UtcNow;
            await ModifyAsync(existing, token);
        }
        await CommitChangesAsync(token);
    }

    public async Task ReopenYearAsync(int year, CancellationToken token)
    {
        var existing = await GetQueryable(x => x.Year == year).FirstOrDefaultAsync(token);
        if (existing == null || !existing.IsLocked)
        {
            throw new ValidationException($"{year} is not currently locked.");
        }
        existing.IsLocked = false;
        existing.ReopenedAt = DateTime.UtcNow;
        await ModifyAsync(existing, token);
        await CommitChangesAsync(token);
    }
}
