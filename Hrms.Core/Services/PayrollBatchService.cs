using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services;

// Owns the PayrollBatch header/master row for each Generate run — see PayrollBatch and
// Payroll.PayrollBatchId. PayrollProcessorService orchestrates this alongside PayrollService
// for Generate/Post/Delete.
public class PayrollBatchService : BaseService<PayrollBatch>
{
    public PayrollBatchService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<PayrollBatch?> FineOneAsync(Guid id, CancellationToken token)
    {
        return await GetOneAsync(id, token);
    }

    // commit=false lets the 4 payroll-generating flows (Regular/13th Month/Last Pay/Year-End
    // Adjustment) compose this with PayrollService.SavePayrollsAsync as ONE atomic unit — same
    // reasoning as PostAsync's commit parameter below. Without this, the batch header commits
    // immediately here while its Payroll rows commit separately afterward; anything that throws
    // in between (a mapping error, a cancelled request) leaves a permanently orphaned
    // PayrollBatch with zero children that can't even be posted.
    public async Task AddAsync(PayrollBatch model, CancellationToken token, bool commit = true)
    {
        await CreateAsync(model, token);
        if (commit) await CommitChangesAsync(token);
        else await SaveChangesAsync(token);
    }

    // commit=false lets PayrollBatchLifecycleService.PostBatchAsync compose this with
    // PayrollService.PostBatchAsync (and, for a YearEndAdjustment batch,
    // YearLockService.LockYearAsync) as ONE atomic unit -- the shared IUnitOfWorkService already
    // has an ambient transaction open for the whole request scope, and its CommitChangesAsync
    // commits (and ends) that transaction outright, so composing multiple CommitChangesAsync
    // calls into one operation would finalize the transaction after the FIRST call and silently
    // drop everything after it. Flushing via SaveChangesAsync here and letting the orchestrator
    // make the one real CommitChangesAsync call at the end keeps the whole sequence atomic.
    public async Task PostAsync(Guid id, CancellationToken token, bool commit = true)
    {
        var batch = await GetOneAsync(id, token);
        if (batch == null) throw new ValidationException("Payroll batch not found.");
        if (batch.IsPosted) return;
        batch.IsPosted = true;
        batch.PostedAt = DateTime.UtcNow;
        await ModifyAsync(batch, token);
        if (commit) await CommitChangesAsync(token);
        else await SaveChangesAsync(token);
    }

    // commit=false lets PayrollBatchLifecycleService.DeleteBatchAsync compose this with the
    // rest of that method's cleanup (contribution ledgers, Payroll/PayrollDtrDetail/
    // PayrollDeductionDetail rows, DTR unpost) as ONE atomic unit -- same reasoning as
    // PostAsync's commit parameter above.
    public async Task DeleteAsync(Guid id, CancellationToken token, bool commit = true)
    {
        await RemoveAsync(id, token);
        if (commit) await CommitChangesAsync(token);
        else await SaveChangesAsync(token);
    }

    // Persists in-memory changes to an already-loaded PayrollBatch -- e.g. ApprovalStatus/
    // PostedBy set by PayrollBatchLifecycleService.ApproveBatchAsync/DeclineBatchAsync. Same
    // commit=false composition reasoning as AddAsync/PostAsync/DeleteAsync above.
    public async Task UpdateAsync(PayrollBatch model, CancellationToken token, bool commit = true)
    {
        await ModifyAsync(model, token);
        if (commit) await CommitChangesAsync(token);
        else await SaveChangesAsync(token);
    }

    // Every DTR batch code that has already been used to generate a payroll, across all
    // past runs — used to block re-generating payroll from a batch that's already used.
    public async Task<HashSet<string>> GetUsedDtrBatchCodesAsync(CancellationToken token)
    {
        var raw = await GetQueryable(x => x.DtrBatchCodes != null && x.DtrBatchCodes != "")
            .Select(x => x.DtrBatchCodes)
            .Distinct()
            .ToListAsync(token);
        return raw
            .SelectMany(x => x!.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet();
    }

    // Batch-list projection for the Payroll Batches tab -- queried directly against PayrollBatch
    // (an overlap filter, not the same "row falls within range" filter PayrollService.GetAsync
    // uses for the per-employee report, since there are no child rows to filter here) rather than
    // derived by grouping child Payroll rows client-side, which is how the UI worked before this.
    // EmployeeCount comes from one grouped count query against Payroll -- avoids an N+1 per batch.
    public async Task<List<PayrollBatchListModel>> GetBatchesAsync(DateOnly from, DateOnly to, CancellationToken token)
    {
        var batches = await GetQueryable(x => x.PayPeriodStart <= to && x.PayPeriodEnd >= from)
            .OrderByDescending(x => x.PayPeriodStart)
            .ToListAsync(token);
        if (batches.Count == 0) return [];

        var batchIds = batches.Select(x => x.Id).ToList();
        var counts = await _uow.Repository.Find<Payroll>(x => batchIds.Contains(x.PayrollBatchId))
            .GroupBy(x => x.PayrollBatchId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, token);

        return batches.Select(b => new PayrollBatchListModel
        {
            Id = b.Id,
            PayPeriodStart = b.PayPeriodStart,
            PayPeriodEnd = b.PayPeriodEnd,
            PayDate = b.PayDate,
            Remarks = b.Remarks,
            EmployeeCount = counts.GetValueOrDefault(b.Id, 0),
            IsPosted = b.IsPosted,
            ApprovalStatus = b.ApprovalStatus,
            GeneratedByEmployeeId = b.GeneratedByEmployeeId,
            PostedBy = b.PostedBy,
            PayrollType = b.PayrollType,
            PendingDeletion = b.PendingDeletion,
            RequestedDeletionByEmployeeId = b.RequestedDeletionByEmployeeId,
        }).ToList();
    }
}
