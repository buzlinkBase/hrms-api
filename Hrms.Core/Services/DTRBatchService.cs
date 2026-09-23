using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// Owns the DTRBatch header/master row for each Save Draft action — see DTRBatch and
// DailyRecordService, which orchestrates this alongside its own DailyRecord writes for
// Save Draft/Approve/Decline/Delete. Mirrors PayrollBatchService's role for Payroll batches.
public class DTRBatchService : BaseService<DTRBatch>
{
    public DTRBatchService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<DTRBatch?> FineOneAsync(Guid id, CancellationToken token)
    {
        return await GetOneAsync(id, token);
    }

    public async Task<List<DTRBatch>> FindByCodesAsync(List<string> batchCodes, CancellationToken token)
    {
        if (batchCodes.Count == 0) return [];
        return await GetQueryable(x => batchCodes.Contains(x.BatchCode)).ToListAsync(token);
    }

    // commit=false lets DailyRecordService.SaveDraftAsync compose this with its own
    // AddRangeAsync/ApprovalEngineService.StartAsync calls as ONE atomic unit -- same reasoning
    // as PayrollBatchService.AddAsync's commit parameter.
    public async Task AddAsync(DTRBatch model, CancellationToken token, bool commit = true)
    {
        await CreateAsync(model, token);
        if (commit) await CommitChangesAsync(token);
        else await SaveChangesAsync(token);
    }

    // commit=false lets DailyRecordService.ApproveBatchAsync/DeclineBatchAsync compose this with
    // the ApprovalEngineService.RecordActionAsync write (and, on final approval, PostAsync) as
    // ONE atomic unit.
    public async Task UpdateAsync(DTRBatch model, CancellationToken token, bool commit = true)
    {
        await ModifyAsync(model, token);
        if (commit) await CommitChangesAsync(token);
        else await SaveChangesAsync(token);
    }

    // Deleting a DTR batch (DailyRecordService.DeleteAsync) removes its DailyRecord rows --
    // this cleans up the now-orphaned header row alongside them. A no-op for a legacy batch
    // that predates this entity (nothing to delete).
    public async Task DeleteByCodeAsync(string batchCode, CancellationToken token, bool commit = true)
    {
        await ExecuteDeleteAsync(x => x.BatchCode == batchCode, token);
        if (commit) await CommitChangesAsync(token);
        else await SaveChangesAsync(token);
    }

    // Called from PayrollBatchLifecycleService.DeleteBatchAsync's DTR-unpost cascade (deleting a
    // payroll draft releases the DTR it was built from) -- ApprovalStatus is left untouched
    // (historical fact preserved), only the posted flag/timestamp revert, same as
    // DailyRecordService.UnpostAsync's own DailyRecord-level unpost.
    public async Task MarkUnpostedByCodeAsync(string batchCode, CancellationToken token, bool commit = true)
    {
        var batch = await GetQueryable(x => x.BatchCode == batchCode, noTracking: false).FirstOrDefaultAsync(token);
        if (batch == null || !batch.IsPosted) return;
        batch.IsPosted = false;
        batch.PostedAt = null;
        await ModifyAsync(batch, token);
        if (commit) await CommitChangesAsync(token);
        else await SaveChangesAsync(token);
    }
}
