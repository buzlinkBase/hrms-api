using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// A saved DTR (draft, for approval, approved or posted -- any DailyRecord with a BatchCode) has the
// punches of an attendance upload baked into it. Deleting the upload underneath it would leave that
// DTR, and any payroll built on it, paying for hours whose logs no longer exist, so the DTR has to
// go first -- same rule as SavedDtrDeletionPolicy for approved applications.
//
// An upload covers, per employee, the dates of their punches in it plus the day before the first
// one (a night shift's time-out lands on the next date). Unregistered logs (no employee) never reach
// a DTR. Confidential employees count: the upload is deleted as a whole.
public static class UploadBatchDtrLock
{
    /// <summary>Upload batch code -> the saved DTR batches covering its logs (only locked uploads are keys).</summary>
    public static async Task<Dictionary<string, List<string>>> FindAsync(
        HrmsContext context,
        IReadOnlyCollection<string> uploadBatches,
        CancellationToken token)
    {
        if (uploadBatches.Count == 0) return [];

        var spans = await context.IgnoringConfidentiality<Attendance>()
            .AsNoTracking()
            .Where(x => x.LogSource == LOGSOURCE.UPLOADED && x.EmployeeId != null && uploadBatches.Contains(x.BatchCode))
            .GroupBy(x => new { x.BatchCode, x.EmployeeId })
            .Select(g => new
            {
                g.Key.BatchCode,
                EmployeeId = g.Key.EmployeeId!.Value,
                First = g.Min(x => x.WorkDateTime),
                Last = g.Max(x => x.WorkDateTime),
            })
            .ToListAsync(token);
        if (spans.Count == 0) return [];

        var employeeIds = spans.Select(x => x.EmployeeId).Distinct().ToList();
        var from = DateOnly.FromDateTime(spans.Min(x => x.First)).AddDays(-1);
        var to = DateOnly.FromDateTime(spans.Max(x => x.Last));

        var saved = await context.IgnoringConfidentiality<DailyRecord>()
            .AsNoTracking()
            .Where(x =>
                employeeIds.Contains(x.EmployeeId) &&
                x.WorkDate  >= from &&
                x.WorkDate  <= to &&
                x.BatchCode != null &&
                x.BatchCode != "")
            .Select(x => new { x.EmployeeId, x.WorkDate, BatchCode = x.BatchCode! })
            .Distinct()
            .ToListAsync(token);
        if (saved.Count == 0) return [];

        var savedByEmployee = saved.ToLookup(x => x.EmployeeId);
        return spans
            .SelectMany(span =>
            {
                var first = DateOnly.FromDateTime(span.First).AddDays(-1);
                var last = DateOnly.FromDateTime(span.Last);
                return savedByEmployee[span.EmployeeId]
                    .Where(x => x.WorkDate >= first && x.WorkDate <= last)
                    .Select(x => (Upload: span.BatchCode, Dtr: x.BatchCode));
            })
            .GroupBy(x => x.Upload)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Dtr).Distinct().OrderBy(x => x).ToList());
    }

    public static async Task EnsureDeletableAsync(HrmsContext context, string uploadBatch, CancellationToken token)
    {
        var locks = await FindAsync(context, [uploadBatch], token);
        if (!locks.TryGetValue(uploadBatch, out var dtrBatches)) return;

        var batches = dtrBatches.Count == 1
            ? $"DTR batch {dtrBatches[0]} already includes"
            : $"DTR batches {string.Join(", ", dtrBatches)} already include";
        throw new ValidationException(
            $"Cannot delete this upload: {batches} its logs. Delete that saved DTR first.");
    }
}
