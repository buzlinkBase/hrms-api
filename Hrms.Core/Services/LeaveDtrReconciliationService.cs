using Hrms.Core.Services.LeaveConsumption;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Services;

// Phase 2 of the Reserve & Consume model.
//
// Phase 1 (approval): DeductCreditsAsync writes a Reserved ledger entry and increments
// LeaveCredits.Reserved — the hard Balance is untouched.
//
// Phase 2 (DTR post): ConsumeReservationsAsync converts reservations into authoritative
// Deduction entries, in DAYS, for the leave days the posted DTR rows actually carry — only for
// applications that are still approved and not deleted (see LeaveConsumptionCalculator).
//
// On unpost / DTR deletion: ReverseConsumptionAsync undoes Phase 2 for that batch only. If the
// leave is still Approved the days go back to Reserved; if Cancelled/Declined/deleted a plain
// Reversal is written.
//
// Every Phase 2 entry (Released, Deduction, Reversal, Reserved-on-unpost, DtrCorrection) carries
// DtrBatchCode, so "what does this batch currently consume for this application" is always
// Σ Deduction − Σ Reversal ± Σ DtrCorrection over that batch's entries. All reversals work from
// that net, which keeps repeated post/unpost cycles, multi-batch leaves and cancellations from
// double-reversing or skipping a batch.
//
// Ledger sum semantics (Σ Amount per employee/leave) stay consistent with LeaveCredits:
// approved → −reserved; posted → −consumed; unposted again → −reserved.
public class LeaveDtrReconciliationService
{
    private readonly IUnitOfWorkService _uow;
    private readonly ILogger<LeaveDtrReconciliationService> _logger;

    public LeaveDtrReconciliationService(
        IUnitOfWorkService uow,
        ILogger<LeaveDtrReconciliationService> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    // Posting, unposting and deleting a DTR batch act on the whole batch, whoever runs them: a
    // confidential employee the approver can't see still has leave days consumed or given back.
    // So the leave records read here skip the confidential-employee filter (company scoping and
    // soft-delete still apply).
    private IQueryable<T> Everyone<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate)
        where T : class, IEntityTenant, ITimeStamp =>
        _uow.Context.IgnoringConfidentiality<T>().Where(predicate);

    public async Task ConsumeReservationsAsync(
        string batchCode,
        List<DailyRecord> records,
        CancellationToken token)
    {
        // PaidLeaveHours is deliberately 0 for a OneTime-payout leave day (paid as a lump sum
        // during a specific payroll run instead — see LeaveApplication.PayoutMode), so leave days
        // are found via LeavesInfo presence, not PaidLeaveHours.
        var leaveRecords = records.Where(r => r.LeavesInfo is { Count: > 0 }).ToList();
        if (leaveRecords.Count == 0) return;

        var applications = await LoadApprovedApplicationsAsync(leaveRecords, token);
        var consumption  = LeaveConsumptionCalculator.Compute(leaveRecords, applications);

        if (consumption.ExcludedDays > 0)
            _logger.LogWarning(
                "DTR post {BatchCode}: {Days} leave day(s) skipped — no approved leave application covers them (deleted, declined, cancelled or pending)",
                batchCode, consumption.ExcludedDays);

        var batchEndDate = leaveRecords.Max(r => r.WorkDate);
        var today        = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var app in applications)
        {
            if (!IsCreditTracked(app)) continue;
            if (!consumption.DaysByApplication.TryGetValue(app.Id, out var days) || days <= 0) continue;

            // Idempotency: this batch already has a live (not reversed) deduction for the leave.
            var batchEntries = await BatchEntriesAsync(app.Id, batchCode, token);
            if (NetConsumed(batchEntries) > 0) continue;

            var credits = await FindCreditsAsync(app, token);
            if (credits == null) continue;

            // Release the soft hold this batch confirms. When the batch reaches the leave's last
            // day, release whatever is left (actual can be less than the estimate); otherwise
            // only this batch's share, so a later batch of the same leave still has its hold.
            var netReserved = await NetReservedAsync(app.Id, token);
            var release = batchEndDate >= app.LeaveDateTo ? netReserved : Math.Min(netReserved, days);
            if (release > 0)
            {
                credits.Reserved -= release;
                AddEntry(credits, app, LedgerEntryType.Released, today, amount: release,
                    $"Reservation released — DTR batch {batchCode}", batchCode);
            }

            credits.Used    += days;
            credits.Balance -= days;
            AddEntry(credits, app, LedgerEntryType.Deduction, today, amount: -days,
                $"DTR-confirmed leave deduction — {app.LeaveDateFrom:MMM dd} to {app.LeaveDateTo:MMM dd, yyyy} (batch {batchCode})",
                batchCode);

            _logger.LogInformation(
                "DTR post {BatchCode}: consumed {Days} day(s) for employee {EmployeeId} (released {Released} reserved)",
                batchCode, days, app.EmployeeId, release);
        }
    }

    // Unpost or deletion of a DTR batch: gives back exactly what this batch still consumes.
    public async Task ReverseConsumptionAsync(string batchCode, CancellationToken token)
    {
        var entries = await Everyone<LeaveLedger>(
            x => x.DtrBatchCode == batchCode && x.ReferenceApplicationId != null)
            .AsNoTracking()
            .ToListAsync(token);
        if (entries.Count == 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var group in entries.GroupBy(x => (AppId: x.ReferenceApplicationId!.Value, x.LeaveCreditsId)))
        {
            var net = NetConsumed(group);
            if (net <= 0) continue;

            var credits = await Everyone<LeaveCredits>(
                x => x.Id == group.Key.LeaveCreditsId)
                .FirstOrDefaultAsync(token);
            if (credits == null) continue;

            var app = await Everyone<LeaveApplication>(
                x => x.Id == group.Key.AppId)
                .FirstOrDefaultAsync(token);
            var first = group.First();

            credits.Used    -= net;
            credits.Balance += net;
            AddEntry(credits, first.EmployeeId, first.LeaveId, group.Key.AppId, LedgerEntryType.Reversal, today,
                amount: net, $"Reversal — DTR batch {batchCode} unposted/deleted", batchCode);

            // Still approved (and not deleted): the leave goes back to waiting for a DTR, so its
            // hold returns — the amount this batch released, falling back to the days consumed.
            if (app is { ApprovalStatus: ApprovalStatus.Approved })
            {
                // Net released by this batch: released (+) plus any earlier re-reserve (−).
                var releasedByBatch = group
                    .Where(x => x.EntryType is LedgerEntryType.Released or LedgerEntryType.Reserved)
                    .Sum(x => x.Amount);
                var reserveBack = releasedByBatch > 0 ? releasedByBatch : net;

                credits.Reserved += reserveBack;
                AddEntry(credits, first.EmployeeId, first.LeaveId, group.Key.AppId, LedgerEntryType.Reserved, today,
                    amount: -reserveBack, $"Re-reserved — DTR batch {batchCode} unposted/deleted", batchCode);
            }

            _logger.LogInformation(
                "DTR batch {BatchCode} reversed: {Days} day(s) for application {AppId} (re-reserved: {Reserved})",
                batchCode, net, group.Key.AppId, app is { ApprovalStatus: ApprovalStatus.Approved });
        }
    }

    // Leave cancelled/declined/deleted after DTRs were posted: reverses every batch that still
    // consumes it, one Reversal per batch (so a later unpost of any of those batches is a no-op).
    public async Task ReverseApplicationDeductionsAsync(LeaveApplication app, string reason, CancellationToken token)
    {
        var entries = await Everyone<LeaveLedger>(
            x => x.ReferenceApplicationId == app.Id && x.DtrBatchCode != null)
            .AsNoTracking()
            .ToListAsync(token);
        if (entries.Count == 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var group in entries.GroupBy(x => (Batch: x.DtrBatchCode!, x.LeaveCreditsId)))
        {
            var net = NetConsumed(group);
            if (net <= 0) continue;

            var credits = await Everyone<LeaveCredits>(
                x => x.Id == group.Key.LeaveCreditsId)
                .FirstOrDefaultAsync(token);
            if (credits == null) continue;

            credits.Used    -= net;
            credits.Balance += net;
            AddEntry(credits, app, LedgerEntryType.Reversal, today, amount: net,
                $"Leave {reason} — reversal of DTR-confirmed {net:0.##} day(s) (batch {group.Key.Batch})", group.Key.Batch);

            _logger.LogInformation(
                "Leave {Reason}: reversed {Days} day(s) of batch {BatchCode} for employee {EmployeeId}",
                reason, net, group.Key.Batch, app.EmployeeId);
        }
    }

    // One-off repair for deductions posted before the hours→days fix (and any other drift):
    // recomputes what each posted batch should consume per application from its DTR rows, and
    // writes a DtrCorrection for the difference. Re-running is a no-op — corrections are part of
    // the net it compares against. A batch whose DTR rows are gone (deleted before deletion
    // rolled back the ledger) should consume 0, so its leftover deduction is reversed too.
    // Also releases reservations still held by leaves that no longer count (see
    // ReleaseStaleReservationsAsync).
    public async Task<List<LeaveDtrRepairLine>> RepairPostedDeductionsAsync(int year, bool dryRun, CancellationToken token)
    {
        var lines = await RepairBatchDeductionsAsync(year, dryRun, token);
        lines.AddRange(await ReleaseStaleReservationsAsync(year, dryRun, token));

        if (!dryRun && lines.Any(l => Math.Abs(l.Delta) >= 0.01m))
            await _uow.CommitChangesAsync("", token);

        return lines;
    }

    /// <summary>BatchCode of the repair lines that release a stale reservation.</summary>
    public const string ReservationRepairBatchCode = "Reservation";

    // Leaves deleted before deletion gave credits back still hold their approval-time
    // reservation: a hard delete nulled their ledger rows' ReferenceApplicationId (SetNull), a
    // soft delete skipped the release. Any Reserved − Released still outstanding for something
    // other than an approved, not-deleted application is released here, per credits row.
    private async Task<List<LeaveDtrRepairLine>> ReleaseStaleReservationsAsync(int year, bool dryRun, CancellationToken token)
    {
        var entries = await Everyone<LeaveLedger>(
            x =>
                x.LeaveCredits.PeriodYear == year &&
                (x.EntryType == LedgerEntryType.Reserved || x.EntryType == LedgerEntryType.Released))
            .AsNoTracking()
            .ToListAsync(token);
        if (entries.Count == 0) return [];

        var referencedIds = entries
            .Where(x => x.ReferenceApplicationId != null)
            .Select(x => x.ReferenceApplicationId!.Value)
            .ToHashSet();
        // The global soft-delete filter leaves deleted applications out.
        var liveIds = (await Everyone<LeaveApplication>(
            x => referencedIds.Contains(x.Id) && x.ApprovalStatus == ApprovalStatus.Approved)
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(token))
            .ToHashSet();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lines = new List<LeaveDtrRepairLine>();

        var stale = entries
            .Where(x => x.ReferenceApplicationId is not { } id || !liveIds.Contains(id))
            .GroupBy(x => (x.ReferenceApplicationId, x.LeaveCreditsId));
        foreach (var group in stale)
        {
            // Reserved rows are negative, Released positive: what's still held is minus their sum.
            var held = -group.Sum(x => x.Amount);
            if (held < 0.01m) continue;

            var first = group.First();
            lines.Add(new LeaveDtrRepairLine(first.EmployeeId, first.LeaveId, ReservationRepairBatchCode,
                group.Key.ReferenceApplicationId ?? Guid.Empty, held, 0m, -held));
            if (dryRun) continue;

            var credits = await Everyone<LeaveCredits>(
                x => x.Id == group.Key.LeaveCreditsId)
                .FirstOrDefaultAsync(token);
            if (credits == null) continue;

            credits.Reserved = Math.Max(0m, credits.Reserved - held);
            _uow.Repository.Add(new LeaveLedger
            {
                Id                     = Guid.NewGuid(),
                EmployeeId             = first.EmployeeId,
                LeaveId                = first.LeaveId,
                LeaveCreditsId         = credits.Id,
                EntryType              = LedgerEntryType.Released,
                EntryDate              = today,
                Amount                 = held,
                Balance                = credits.Balance,
                Particulars            = "Reservation released — leave no longer active (repair)",
                ReferenceApplicationId = group.Key.ReferenceApplicationId,
            });
        }

        return lines;
    }

    private async Task<List<LeaveDtrRepairLine>> RepairBatchDeductionsAsync(int year, bool dryRun, CancellationToken token)
    {
        var entries = await Everyone<LeaveLedger>(
            x =>
                x.DtrBatchCode != null &&
                x.ReferenceApplicationId != null &&
                x.LeaveCredits.PeriodYear == year &&
                (x.EntryType == LedgerEntryType.Deduction ||
                 x.EntryType == LedgerEntryType.Reversal ||
                 x.EntryType == LedgerEntryType.DtrCorrection))
            .AsNoTracking()
            .ToListAsync(token);
        if (entries.Count == 0) return [];

        var groups = entries
            .GroupBy(x => (Batch: x.DtrBatchCode!, AppId: x.ReferenceApplicationId!.Value, x.LeaveCreditsId))
            .ToList();

        // Correct consumption, per batch, from the batch's currently posted DTR rows.
        var targets = new Dictionary<(string Batch, Guid AppId), decimal>();
        foreach (var batchCode in groups.Select(g => g.Key.Batch).Distinct())
        {
            var records = await Everyone<DailyRecord>(
                x => x.BatchCode == batchCode && x.Posted)
                .Include(x => x.LeavesInfo)
                .AsNoTracking()
                .ToListAsync(token);
            var leaveRecords = records.Where(r => r.LeavesInfo is { Count: > 0 }).ToList();
            if (leaveRecords.Count == 0) continue;

            var applications = await LoadApprovedApplicationsAsync(leaveRecords, token);
            var consumption  = LeaveConsumptionCalculator.Compute(leaveRecords, applications);
            foreach (var app in applications.Where(IsCreditTracked))
            {
                if (consumption.DaysByApplication.TryGetValue(app.Id, out var days))
                    targets[(batchCode, app.Id)] = days;
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lines = new List<LeaveDtrRepairLine>();

        foreach (var group in groups)
        {
            var current = NetConsumed(group);
            var correct = targets.GetValueOrDefault((group.Key.Batch, group.Key.AppId));
            var delta   = correct - current;
            var first   = group.First();

            lines.Add(new LeaveDtrRepairLine(first.EmployeeId, first.LeaveId, group.Key.Batch, group.Key.AppId, current, correct, delta));
            if (dryRun || Math.Abs(delta) < 0.01m) continue;

            var credits = await Everyone<LeaveCredits>(
                x => x.Id == group.Key.LeaveCreditsId)
                .FirstOrDefaultAsync(token);
            if (credits == null) continue;

            // delta > 0: under-charged, consume more; delta < 0: over-charged, give back.
            credits.Used    += delta;
            credits.Balance -= delta;
            AddEntry(credits, first.EmployeeId, first.LeaveId, group.Key.AppId, LedgerEntryType.DtrCorrection, today,
                amount: -delta,
                $"DTR correction — batch {group.Key.Batch}: {current:0.##} → {correct:0.##} day(s)", group.Key.Batch);
        }

        return lines;
    }

    // Per employee + leave type totals of RepairPostedDeductionsAsync's lines, for the preview/
    // result table HR reviews (the per-batch lines are too granular to read).
    public async Task<List<LeaveDtrRepairSummary>> SummarizeAsync(List<LeaveDtrRepairLine> lines, CancellationToken token)
    {
        if (lines.Count == 0) return [];

        var employeeIds = lines.Select(l => l.EmployeeId).ToHashSet();
        var leaveIds    = lines.Select(l => l.LeaveId).ToHashSet();
        var employees = await _uow.Repository.Find<Employee>(e => employeeIds.Contains(e.Id))
            .AsNoTracking()
            .Select(e => new { e.Id, e.EmployeeNo, e.FirstName, e.LastName })
            .ToDictionaryAsync(e => e.Id, token);
        var leaves = await _uow.Repository.Find<Leave>(l => leaveIds.Contains(l.Id))
            .AsNoTracking()
            .Select(l => new { l.Id, l.Code, l.Description })
            .ToDictionaryAsync(l => l.Id, token);

        return lines
            .GroupBy(l => (l.EmployeeId, l.LeaveId))
            .Select(g =>
            {
                var employee = employees.GetValueOrDefault(g.Key.EmployeeId);
                var leave    = leaves.GetValueOrDefault(g.Key.LeaveId);
                return new LeaveDtrRepairSummary(
                    g.Key.EmployeeId,
                    employee?.EmployeeNo ?? "",
                    employee == null ? "" : $"{employee.FirstName} {employee.LastName}".Trim(),
                    g.Key.LeaveId,
                    leave?.Code ?? "",
                    leave?.Description ?? "",
                    g.Sum(x => x.CurrentDays),
                    g.Sum(x => x.CorrectDays),
                    g.Sum(x => x.Delta));
            })
            .Where(s => Math.Abs(s.Delta) >= 0.01m)
            .OrderBy(s => s.FullName)
            .ThenBy(s => s.LeaveCode)
            .ToList();
    }

    // ---- helpers ----

    // Government-paid and without-pay leaves never touch company leave credits.
    private static bool IsCreditTracked(LeaveApplication app) =>
        app.Leave?.PaySource != PaySource.Government && app.PayType != PayType.WithoutPay;

    // Approved and (via the global soft-delete filter) not deleted — the only applications that
    // may consume credits.
    private async Task<List<LeaveApplication>> LoadApprovedApplicationsAsync(List<DailyRecord> leaveRecords, CancellationToken token)
    {
        var employeeIds = leaveRecords.Select(r => r.EmployeeId).ToHashSet();
        var minDate     = leaveRecords.Min(r => r.WorkDate);
        var maxDate     = leaveRecords.Max(r => r.WorkDate);

        return await Everyone<LeaveApplication>(
            x =>
                employeeIds.Contains(x.EmployeeId) &&
                x.LeaveDateFrom  <= maxDate &&
                x.LeaveDateTo    >= minDate &&
                x.ApprovalStatus == ApprovalStatus.Approved)
            .Include(x => x.Leave)
            .ToListAsync(token);
    }

    private Task<List<LeaveLedger>> BatchEntriesAsync(Guid appId, string batchCode, CancellationToken token) =>
        Everyone<LeaveLedger>(
            x => x.ReferenceApplicationId == appId && x.DtrBatchCode == batchCode)
            .AsNoTracking()
            .ToListAsync(token);

    // What a batch currently consumes for one application: minus the signed sum of its
    // Deduction (−), Reversal (+) and DtrCorrection (±) rows.
    internal static decimal NetConsumed(IEnumerable<LeaveLedger> batchEntries) =>
        -batchEntries
            .Where(e => e.EntryType is LedgerEntryType.Deduction or LedgerEntryType.Reversal or LedgerEntryType.DtrCorrection)
            .Sum(e => e.Amount);

    private async Task<decimal> NetReservedAsync(Guid appId, CancellationToken token)
    {
        var entries = await Everyone<LeaveLedger>(
            x =>
                x.ReferenceApplicationId == appId &&
                (x.EntryType == LedgerEntryType.Reserved || x.EntryType == LedgerEntryType.Released))
            .AsNoTracking()
            .ToListAsync(token);
        // Reserved rows are negative, Released positive.
        return -entries.Sum(x => x.Amount);
    }

    private Task<LeaveCredits?> FindCreditsAsync(LeaveApplication app, CancellationToken token) =>
        Everyone<LeaveCredits>(
            x =>
                x.EmployeeId == app.EmployeeId &&
                x.LeaveId    == app.LeaveId    &&
                x.PeriodYear == app.LeaveDateFrom.Year)
            .FirstOrDefaultAsync(token);

    private void AddEntry(LeaveCredits credits, LeaveApplication app, LedgerEntryType type, DateOnly date,
        decimal amount, string particulars, string batchCode) =>
        AddEntry(credits, app.EmployeeId, app.LeaveId, app.Id, type, date, amount, particulars, batchCode);

    private void AddEntry(LeaveCredits credits, Guid employeeId, Guid leaveId, Guid appId, LedgerEntryType type,
        DateOnly date, decimal amount, string particulars, string batchCode) =>
        _uow.Repository.Add(new LeaveLedger
        {
            Id                     = Guid.NewGuid(),
            EmployeeId             = employeeId,
            LeaveId                = leaveId,
            LeaveCreditsId         = credits.Id,
            EntryType              = type,
            EntryDate              = date,
            Amount                 = amount,
            Balance                = credits.Balance,
            Particulars            = particulars,
            ReferenceApplicationId = appId,
            DtrBatchCode           = batchCode,
        });
}

/// <summary>One application's consumption for one DTR batch, before/after the repair.</summary>
public record LeaveDtrRepairLine(
    Guid EmployeeId, Guid LeaveId, string BatchCode, Guid ApplicationId,
    decimal CurrentDays, decimal CorrectDays, decimal Delta);

/// <summary>Repair preview/result row: one employee's leave type, summed across batches. Delta
/// &gt; 0 means more days are charged after the repair; &lt; 0 means days are given back.</summary>
public record LeaveDtrRepairSummary(
    Guid EmployeeId, string EmployeeNo, string FullName,
    Guid LeaveId, string LeaveCode, string LeaveDescription,
    decimal CurrentDays, decimal CorrectDays, decimal Delta);
