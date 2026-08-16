using Hrms.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Services;

// Phase 2 of the Reserve & Consume model.
//
// Phase 1 (approval): DeductCreditsAsync writes a Reserved ledger entry and increments
// LeaveCredits.Reserved — the hard Balance is untouched.
//
// Phase 2 (DTR post): ConsumeReservationsAsync converts reservations into authoritative
// Deduction entries using CreditsSpent from each DailyRecord. The reservation is released
// and Used/Balance are updated with the DTR-confirmed amounts.
//
// On unpost: ReverseConsumptionAsync undoes Phase 2. If the leave is still Approved the
// days go back to Reserved; if Cancelled/Declined a plain Reversal is written.
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

    public async Task ConsumeReservationsAsync(
        string batchCode,
        List<DailyRecord> records,
        CancellationToken token)
    {
        var leaveRecords = records.Where(r => r.CreditsSpent > 0).ToList();
        if (leaveRecords.Count == 0) return;

        var employeeIds = leaveRecords.Select(r => r.EmployeeId).ToHashSet();
        var minDate     = leaveRecords.Min(r => r.WorkDate);
        var maxDate     = leaveRecords.Max(r => r.WorkDate);
        var today       = DateTime.UtcNow;

        var applications = await _uow.Repository
            .Find<LeaveApplication>(x =>
                employeeIds.Contains(x.EmployeeId) &&
                x.LeaveDateFrom  <= maxDate         &&
                x.LeaveDateTo    >= minDate         &&
                x.ApprovalStatus == ApprovalStatus.Approved)
            .Include(x => x.Leave)
            .ToListAsync(token);

        foreach (var app in applications)
        {
            if (app.Leave?.PaySource == PaySource.Government) continue;
            if (app.PayType == PayType.WithoutPay) continue;

            var appRecords = leaveRecords
                .Where(r => r.EmployeeId == app.EmployeeId &&
                            r.WorkDate   >= app.LeaveDateFrom &&
                            r.WorkDate   <= app.LeaveDateTo)
                .ToList();

            if (appRecords.Count == 0) continue;

            var actualConsumed = (decimal)appRecords.Sum(r => r.CreditsSpent);
            if (actualConsumed <= 0) continue;

            // Idempotency: don't write a second Deduction for the same batch + application
            var alreadyConsumed = await _uow.Repository
                .Find<LeaveLedger>(x =>
                    x.ReferenceApplicationId == app.Id  &&
                    x.DtrBatchCode           == batchCode &&
                    x.EntryType              == LedgerEntryType.Deduction)
                .AnyAsync(token);

            if (alreadyConsumed) continue;

            var credits = await _uow.Repository
                .Find<LeaveCredits>(x =>
                    x.EmployeeId == app.EmployeeId &&
                    x.LeaveId    == app.LeaveId    &&
                    x.PeriodYear == app.LeaveDateFrom.Year)
                .FirstOrDefaultAsync(token);

            if (credits == null) continue;

            // Compute net outstanding reservation for this application
            var reservedSum = await _uow.Repository
                .Find<LeaveLedger>(x =>
                    x.ReferenceApplicationId == app.Id &&
                    x.EntryType              == LedgerEntryType.Reserved)
                .SumAsync(x => x.Less, token);

            var releasedSum = await _uow.Repository
                .Find<LeaveLedger>(x =>
                    x.ReferenceApplicationId == app.Id &&
                    x.EntryType              == LedgerEntryType.Released)
                .SumAsync(x => x.Add, token);

            var netReserved = reservedSum - releasedSum;

            // Release the outstanding soft hold (if any) — the actual Deduction below is authoritative
            if (netReserved > 0)
            {
                credits.Reserved -= netReserved;

                _uow.Repository.Add(new LeaveLedger
                {
                    Id                     = Guid.NewGuid(),
                    EmployeeId             = app.EmployeeId,
                    LeaveId                = app.LeaveId,
                    LeaveCreditsId         = credits.Id,
                    EntryType              = LedgerEntryType.Released,
                    EntryDate              = DateOnly.FromDateTime(today),
                    Add                    = netReserved,
                    Less                   = 0m,
                    Balance                = credits.Balance,
                    Particulars            = $"Reservation released — DTR batch {batchCode}",
                    ReferenceApplicationId = app.Id,
                    DtrBatchCode           = batchCode,
                });
            }

            // Write the authoritative deduction using DTR-confirmed actual days
            credits.Used    += actualConsumed;
            credits.Balance -= actualConsumed;

            _uow.Repository.Add(new LeaveLedger
            {
                Id                     = Guid.NewGuid(),
                EmployeeId             = app.EmployeeId,
                LeaveId                = app.LeaveId,
                LeaveCreditsId         = credits.Id,
                EntryType              = LedgerEntryType.Deduction,
                EntryDate              = DateOnly.FromDateTime(today),
                Add                    = 0m,
                Less                   = actualConsumed,
                Balance                = credits.Balance,
                Particulars            = $"DTR-confirmed leave deduction — {app.LeaveDateFrom:MMM dd} to {app.LeaveDateTo:MMM dd, yyyy} (batch {batchCode})",
                ReferenceApplicationId = app.Id,
                DtrBatchCode           = batchCode,
            });

            _logger.LogInformation(
                "DTR post {BatchCode}: consumed {Actual} day(s) for employee {EmployeeId} " +
                "(reserved was {Reserved}, delta returned to balance: {Delta})",
                batchCode, actualConsumed, app.EmployeeId, netReserved, netReserved - actualConsumed);
        }
    }

    public async Task ReverseConsumptionAsync(string batchCode, CancellationToken token)
    {
        var deductions = await _uow.Repository
            .Find<LeaveLedger>(x =>
                x.DtrBatchCode == batchCode &&
                x.EntryType    == LedgerEntryType.Deduction)
            .ToListAsync(token);

        if (deductions.Count == 0) return;

        var today = DateTime.UtcNow;

        foreach (var deduction in deductions)
        {
            // Skip if RestoreCreditsAsync already wrote a Reversal when the leave was cancelled
            // while the DTR was posted — prevents double-reversal.
            var alreadyReversed = await _uow.Repository
                .Find<LeaveLedger>(x =>
                    x.ReferenceApplicationId == deduction.ReferenceApplicationId &&
                    x.EntryType              == LedgerEntryType.Reversal)
                .AnyAsync(token);

            if (alreadyReversed) continue;

            var credits = await _uow.Repository
                .Find<LeaveCredits>(x => x.Id == deduction.LeaveCreditsId)
                .FirstOrDefaultAsync(token);

            if (credits == null) continue;

            credits.Used    -= deduction.Less;
            credits.Balance += deduction.Less;

            var appStillApproved = deduction.ReferenceApplicationId.HasValue &&
                await _uow.Repository
                    .Find<LeaveApplication>(x =>
                        x.Id             == deduction.ReferenceApplicationId.Value &&
                        x.ApprovalStatus == ApprovalStatus.Approved)
                    .AnyAsync(token);

            if (appStillApproved)
            {
                // Restore the ORIGINAL reservation amount, not just the actual consumed days.
                // When this batch posted, it wrote a Released entry for the full estimated reservation.
                // Using only deduction.Less (actual days) would under-reserve if actual < estimated,
                // inflating AvailableToFile while the leave is still pending.
                var originalReservation = await _uow.Repository
                    .Find<LeaveLedger>(x =>
                        x.ReferenceApplicationId == deduction.ReferenceApplicationId &&
                        x.DtrBatchCode           == batchCode &&
                        x.EntryType              == LedgerEntryType.Released)
                    .SumAsync(x => x.Add, token);

                // Fall back to actual days if no Released entry exists (e.g. second batch in a multi-batch leave)
                var reserveBack = originalReservation > 0 ? originalReservation : deduction.Less;

                credits.Reserved += reserveBack;

                _uow.Repository.Add(new LeaveLedger
                {
                    Id                     = Guid.NewGuid(),
                    EmployeeId             = deduction.EmployeeId,
                    LeaveId                = deduction.LeaveId,
                    LeaveCreditsId         = credits.Id,
                    EntryType              = LedgerEntryType.Reserved,
                    EntryDate              = DateOnly.FromDateTime(today),
                    Add                    = 0m,
                    Less                   = reserveBack,
                    Balance                = credits.Balance,
                    Particulars            = $"Re-reserved on DTR unpost — batch {batchCode}",
                    ReferenceApplicationId = deduction.ReferenceApplicationId,
                    DtrBatchCode           = batchCode,
                });
            }
            else
            {
                // Leave cancelled/declined — plain reversal, no re-reservation
                _uow.Repository.Add(new LeaveLedger
                {
                    Id                     = Guid.NewGuid(),
                    EmployeeId             = deduction.EmployeeId,
                    LeaveId                = deduction.LeaveId,
                    LeaveCreditsId         = credits.Id,
                    EntryType              = LedgerEntryType.Reversal,
                    EntryDate              = DateOnly.FromDateTime(today),
                    Add                    = deduction.Less,
                    Less                   = 0m,
                    Balance                = credits.Balance,
                    Particulars            = $"Reversal on DTR unpost — batch {batchCode}",
                    ReferenceApplicationId = deduction.ReferenceApplicationId,
                    DtrBatchCode           = batchCode,
                });
            }

            _logger.LogInformation(
                "DTR unpost {BatchCode}: reversed {Days} day(s) for application {AppId} (re-reserved: {Restored})",
                batchCode, deduction.Less, deduction.ReferenceApplicationId, appStillApproved);
        }
    }
}
