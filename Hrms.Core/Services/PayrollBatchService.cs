using Hrms.Domain.Entities;

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

    public async Task AddAsync(PayrollBatch model, CancellationToken token)
    {
        await CreateAsync(model, token);
        await CommitChangesAsync(token);
    }

    public async Task PostAsync(Guid id, CancellationToken token)
    {
        var batch = await GetOneAsync(id, token);
        if (batch == null) throw new ValidationException("Payroll batch not found.");
        if (batch.IsPosted) return;
        batch.IsPosted = true;
        batch.PostedAt = DateTime.UtcNow;
        await ModifyAsync(batch, token);
        await CommitChangesAsync(token);
    }

    public async Task DeleteAsync(Guid id, CancellationToken token)
    {
        await RemoveAsync(id, token);
        await CommitChangesAsync(token);
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
}
