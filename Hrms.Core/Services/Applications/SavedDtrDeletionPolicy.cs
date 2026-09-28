using Hrms.Domain.Entities;

namespace Hrms.Core.Services.Applications;

// An approved application's hours are baked into every DTR saved for its days (draft, for
// approval, approved or posted alike -- a draft carries them into payroll once it's approved).
// Deleting the application underneath such a DTR would leave that DTR, and any payroll built on
// it, paying for something that no longer exists, so the DTR has to go first. Pending, declined
// and cancelled applications never reached a DTR and stay deletable.
public static class SavedDtrDeletionPolicy
{
    public static async Task EnsureDeletableAsync(
        IQueryable<DailyRecord> dailyRecords,
        ApprovalStatus status,
        Guid employeeId,
        DateOnly from,
        DateOnly to,
        string applicationLabel,
        CancellationToken token)
    {
        if (status != ApprovalStatus.Approved) return;

        var batchCodes = await dailyRecords
            .AsNoTracking()
            .Where(x =>
                x.EmployeeId == employeeId &&
                x.WorkDate   >= from &&
                x.WorkDate   <= to &&
                x.BatchCode  != null &&
                x.BatchCode  != "")
            .Select(x => x.BatchCode!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(token);

        if (batchCodes.Count == 0) return;

        var batches = batchCodes.Count == 1
            ? $"DTR batch {batchCodes[0]} already includes"
            : $"DTR batches {string.Join(", ", batchCodes)} already include";
        throw new InvalidOperationException(
            $"Cannot delete this approved {applicationLabel}: {batches} its date(s). " +
            "Delete or regenerate that DTR first.");
    }
}
