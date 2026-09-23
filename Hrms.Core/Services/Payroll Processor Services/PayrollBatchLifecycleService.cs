using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// Post/Delete are run-level (whole PayrollBatch) transactions, never per-employee ones — an
// employee's payroll is never generated on its own, so it isn't posted or deleted on its own
// either. Extracted from PayrollProcessorService as its own lifecycle concern, independent of
// which flavor of payroll (regular/13th Month/Last Pay) produced the batch.
public class PayrollBatchLifecycleService
{
    private readonly IUnitOfWorkService _uow;
    private readonly PayrollBatchService _payrollBatchService;
    private readonly PayrollService _payrollService;
    private readonly SSSContributionService _sssContributionService;
    private readonly PHICContributionService _phicContributionService;
    private readonly HDMFContributionService _hdmfContributionService;
    private readonly TaxContributionService _taxContributionService;
    private readonly DailyRecordService _dtrServie;
    private readonly PayrollInputConsumptionService _consumptionService;
    private readonly YearLockService _yearLockService;
    private readonly ApprovalEngineService _approvalEngine;
    private readonly DTRBatchService _dtrBatchService;

    public PayrollBatchLifecycleService(
        IUnitOfWorkService uow,
        PayrollBatchService payrollBatchService,
        PayrollService payrollService,
        SSSContributionService sssContributionService,
        PHICContributionService phicContributionService,
        HDMFContributionService hdmfContributionService,
        TaxContributionService taxContributionService,
        DailyRecordService dtrServie,
        PayrollInputConsumptionService consumptionService,
        YearLockService yearLockService,
        ApprovalEngineService approvalEngine,
        DTRBatchService dtrBatchService)
    {
        _uow = uow;
        _payrollBatchService = payrollBatchService;
        _payrollService = payrollService;
        _sssContributionService = sssContributionService;
        _phicContributionService = phicContributionService;
        _hdmfContributionService = hdmfContributionService;
        _taxContributionService = taxContributionService;
        _dtrServie = dtrServie;
        _consumptionService = consumptionService;
        _yearLockService = yearLockService;
        _approvalEngine = approvalEngine;
        _dtrBatchService = dtrBatchService;
    }

    // Locks a whole Generate run in as final — an employee's payroll is never posted on its
    // own, since it was never generated on its own either. Updates the canonical
    // PayrollBatch.IsPosted plus each child Payroll row's denormalized copy (see
    // Payroll.PayrollBatchId doc comment) so existing per-row report filters keep working
    // unchanged. Posting a YearEndAdjustment batch additionally locks that calendar year (its
    // PayPeriodStart is always Jan 1 of the target year — see TaxAnnualizationService.GenerateAsync)
    // so no further Regular/13th-Month/Last-Pay/Year-End-Adjustment data can be generated or
    // deleted for it without an explicit Reopen Year — see YearLockService.
    //
    // Runs both PostAsync calls (and, for a YearEndAdjustment batch, the year lock) as ONE
    // atomic unit: PayrollBatchService.PostAsync flips PayrollBatch.IsPosted first, then
    // PayrollService.PostBatchAsync does the heavier per-employee work (deduction balances,
    // retirement fund) -- until now these were two INDEPENDENT commits. If the second call threw
    // -- e.g. two overlapping Post attempts racing on the same employee's RetirementFund row --
    // the first call's commit had already survived, uncommitted-but-durable. On retry,
    // PayrollBatchService.PostAsync's own `if (batch.IsPosted) return;` guard then silently
    // skipped stage one and only re-ran stage two, which is exactly what made a failed Post look
    // like it needed "posting twice" to succeed.
    //
    // IUnitOfWorkService (BuzlinkRepository) keeps one ambient DB transaction open for its whole
    // scoped lifetime (started when it's constructed, i.e. for the rest of this request), and
    // CommitChangesAsync both flushes AND commits/ends that transaction -- it's meant to be
    // called once, to finalize a unit of work. Composing two independent CommitChangesAsync
    // calls (the old code) silently finalized the transaction after the FIRST call, so the
    // second's changes landed outside any real transaction. The commit:false overloads below
    // flush via SaveChangesAsync instead, without touching the transaction, so every step here
    // joins the SAME still-open transaction; the one real CommitChangesAsync call at the end
    // finalizes all of it together, and any exception before that point rolls the whole thing
    // back via the still-open ambient transaction.
    // Gated behind the PayrollPosting approval instance below -- no endpoint bypasses the
    // approval engine, same principle as every other application type. Only reachable once
    // ApproveBatchAsync's RecordActionAsync call resolves the instance to Approved.
    private async Task ApplyPostAsync(PayrollBatch batch, CancellationToken token)
    {
        await _payrollBatchService.PostAsync(batch.Id, token, commit: false);
        await _payrollService.PostBatchAsync(batch.Id, token, commit: false);

        if (batch.PayrollType == PayrollType.YearEndAdjustment)
        {
            await _yearLockService.LockYearAsync(batch.PayPeriodStart.Year, token, commit: false);
        }
    }

    // One shared ApprovalApplicationType.PayrollPosting approval type covers all 4 run types
    // (Regular/13th Month/Last Pay/Year-End Adjustment share this same entity/code path) -- see
    // PayrollBatch.ApprovalStatus. Runs RecordActionAsync plus (on the final step) the existing
    // ApplyPostAsync body as ONE atomic unit, same transaction-composition reasoning as
    // DeleteBatchAsync below: every step flushes via commit:false/SaveChangesAsync and joins the
    // same still-open ambient transaction; the one real CommitChangesAsync call at the end
    // finalizes all of it together, and any exception before that point rolls the whole thing
    // back via the still-open ambient transaction.
    public async Task ApproveBatchAsync(Guid payrollBatchId, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var batch = await _payrollBatchService.FineOneAsync(payrollBatchId, token)
            ?? throw new NotFoundException("Payroll batch not found.");
        if (batch.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This payroll run is not awaiting approval.");
        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

        try
        {
            var result = await _approvalEngine.RecordActionAsync(
                ApprovalApplicationType.PayrollPosting, batch.Id, batch.GeneratedByEmployeeId,
                approverId, approverHasOverride, ApprovalActionType.Approved, note, token);

            batch.ApprovalStatus = ApprovalEngineService.MapInstanceStatus(result.InstanceStatus);
            if (batch.ApprovalStatus == ApprovalStatus.Approved)
            {
                batch.PostedBy = approverId;
                await _payrollBatchService.UpdateAsync(batch, token, commit: false);
                await ApplyPostAsync(batch, token);
            }
            else
            {
                // More steps remain -- stays ForApproval, not posted yet.
                await _payrollBatchService.UpdateAsync(batch, token, commit: false);
            }

            await _payrollBatchService.CommitChangesAsync(token);
        }
        catch
        {
            if (_uow.CurrentTransaction != null)
            {
                await _uow.CurrentTransaction.RollbackAsync(token);
            }
            throw;
        }
    }

    public async Task DeclineBatchAsync(Guid payrollBatchId, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var batch = await _payrollBatchService.FineOneAsync(payrollBatchId, token)
            ?? throw new NotFoundException("Payroll batch not found.");
        if (batch.ApprovalStatus != ApprovalStatus.ForApproval)
            throw new InvalidOperationException("This payroll run is not awaiting approval.");
        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

        try
        {
            var result = await _approvalEngine.RecordActionAsync(
                ApprovalApplicationType.PayrollPosting, batch.Id, batch.GeneratedByEmployeeId,
                approverId, approverHasOverride, ApprovalActionType.Declined, note, token);

            batch.ApprovalStatus = ApprovalEngineService.MapInstanceStatus(result.InstanceStatus);
            await _payrollBatchService.UpdateAsync(batch, token, commit: false);
            await _payrollBatchService.CommitChangesAsync(token);
        }
        catch
        {
            if (_uow.CurrentTransaction != null)
            {
                await _uow.CurrentTransaction.RollbackAsync(token);
            }
            throw;
        }
    }

    // Deletes every row from one Generate run (same PayrollBatchId) in a single action, plus
    // the PayrollBatch header row itself. Post and Delete are run-level transactions, not
    // per-employee ones — an employee's payroll was never generated on its own, so it isn't
    // posted or deleted on its own either. Regenerating is blocked while ANY row from the
    // run's DTR batch(es) still exists (see PayrollProcessorService.GenerateAsync/
    // GetUsedDtrBatchCodesAsync), so a partial per-row cleanup wouldn't actually unblock a
    // re-run anyway. Also removes the SSS/PHIC/HDMF/WTax ledger rows
    // StatutoryContributionLedgerService wrote for this batch, since those are written
    // unconditionally regardless of IsPosted and the remittance reports read them directly
    // rather than filtering by Payroll — leaving them behind would show contributions for a
    // payroll run that no longer exists. Each cascade delete is a single ExecuteDeleteAsync
    // keyed on PayrollBatchId (not a per-employee loop — these 4 tables can reach millions of
    // rows, so this matters), matching how PayrollService.DeleteByBatchIdAsync already deletes
    // the Payroll rows themselves. If the batch has already been posted, it's left alone — a
    // posted run is final.
    //
    // Runs the whole sequence as ONE atomic unit, same fix as PostBatchAsync above and for the
    // identical reason: composing multiple independent CommitChangesAsync calls (the old code
    // had three -- one after the Payroll/contribution-ledger deletes, one inside DTR
    // UnpostAsync per batch code, one inside PayrollBatchService.DeleteAsync, plus a fourth
    // redundant one right after) silently finalized the ambient transaction after the FIRST
    // call. Every step after that point -- including the PayrollBatch header row's own removal,
    // the very last operation in the method -- ran against an already-closed transaction, which
    // is why the header row was never actually observed to be deleted even though the code
    // "looked" like it deleted it. Every step here now flushes via commit:false/SaveChangesAsync
    // and joins the same still-open transaction; the one real CommitChangesAsync call at the end
    // finalizes all of it together, and any exception before that point rolls the whole thing
    // back via the still-open ambient transaction.
    public async Task DeleteBatchAsync(Guid payrollBatchId, CancellationToken token)
    {
        var batch = await _payrollBatchService.FineOneAsync(payrollBatchId, token);
        // Already gone -- a second click, a stale list, or a concurrent delete got there
        // first. The caller's desired end state (this run no longer exists) already holds,
        // so this is a no-op success, not an error.
        if (batch == null) return;
        if (batch.IsPosted)
            throw new ValidationException("This payroll run has already been posted — request its deletion for approval instead.");

        try
        {
            await ExecuteBatchDeletionAsync(batch, token);
            await _payrollBatchService.CommitChangesAsync(token);
        }
        catch
        {
            if (_uow.CurrentTransaction != null)
            {
                await _uow.CurrentTransaction.RollbackAsync(token);
            }
            throw;
        }
    }

    // The actual cascade (year-lock check, contribution-ledger/Payroll/DTR-detail cleanup, DTR
    // unpost, PayrollBatch header removal), shared by DeleteBatchAsync above (an unposted draft)
    // and ApproveDeletionAsync below (an already-posted batch, once its own deletion request
    // clears approval). commit:false throughout -- caller owns the transaction boundary and the
    // one final CommitChangesAsync call, same reasoning as every other commit:false composition
    // in this class.
    private async Task ExecuteBatchDeletionAsync(PayrollBatch batch, CancellationToken token)
    {
        var payrollBatchId = batch.Id;
        // Captured before deletion — releases whatever SalaryAdjustment/OtherIncomeSchedules
        // rows this batch's own Payroll rows had claimed (see PayrollInputConsumptionService),
        // the same "undo what this run reserved" logic already applied below to DTR posting.
        // Also doubles as the source for the year-lock check right below, so this run can't be
        // deleted if its own PostingPeriod falls in a year some batch's posting has already
        // locked -- including its own, for an already-posted YearEndAdjustment batch reaching
        // here via ApproveDeletionAsync; Reopen Year is the existing escape hatch for that case,
        // unchanged by this feature.
        var payrollRows = await _payrollService.GetByBatchIdAsync(payrollBatchId, token);
        var touchedYears = payrollRows.Select(x => x.PostingPeriod.Year).Distinct().ToList();
        foreach (var year in touchedYears)
        {
            if (await _yearLockService.IsYearLockedAsync(year, token))
            {
                throw new ValidationException(
                    $"Payroll for {year} is locked — the Year-End Tax Adjustment has already been posted for " +
                    "this year. Reopen the year first if changes are required.");
            }
        }
        var payrollIds = payrollRows.Select(x => x.Id).ToList();

        await _sssContributionService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _phicContributionService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _hdmfContributionService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _taxContributionService.DeleteByBatchIdAsync(payrollBatchId, token);
        // Never touched a loan's Balance (only PostBatchAsync does) — just cleanup of the
        // now-orphaned per-installment breakdown/DTR-detail rows this run wrote at Generate time.
        await _payrollService.DeleteDeductionDetailsByPayrollIdsAsync(payrollIds, token);
        await _payrollService.DeleteDtrDetailsByPayrollIdsAsync(payrollIds, token);
        await _payrollService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _consumptionService.ReleaseAsync(payrollIds, token);

        // Deleting a run releases the DTR it was built from — unposts every DTR batch this run
        // used (idempotent/no-op on an already-unposted batch), the inverse of
        // PayrollProcessorService.GenerateAsync's post-on-save.
        var dtrBatchCodes = (batch.DtrBatchCodes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var batchCode in dtrBatchCodes)
        {
            await _dtrServie.UnpostAsync(batchCode, token, commit: false);
            // Mirrors the DailyRecord-level unpost above onto the DTRBatch header row --
            // ApprovalStatus is left untouched (historical fact preserved), same reasoning
            // as PayrollBatch keeping ApprovalStatus=Approved after an Unpost/Delete cycle.
            await _dtrBatchService.MarkUnpostedByCodeAsync(batchCode, token, commit: false);
        }

        await _payrollBatchService.DeleteAsync(payrollBatchId, token, commit: false);
    }

    // Requesting deletion of an already-posted batch starts a separate PayrollPostingDeletion
    // approval instance instead of deleting outright (a resolved PayrollPosting instance can't be
    // reopened for a second approval cycle -- see ApprovalApplicationType.PayrollPostingDeletion).
    // The batch stays fully visible/usable (ApprovalStatus stays Approved, Payroll rows
    // untouched) while the request is pending -- PendingDeletion is purely informational until
    // it clears. Mirrors DailyRecordService.RequestDeletionAsync exactly.
    public async Task RequestDeletionAsync(Guid payrollBatchId, Guid requestedByEmployeeId, CancellationToken token)
    {
        var batch = await _payrollBatchService.FineOneAsync(payrollBatchId, token)
            ?? throw new NotFoundException("Payroll batch not found.");
        if (batch.ApprovalStatus != ApprovalStatus.Approved || !batch.IsPosted)
            throw new InvalidOperationException("Only an already-posted payroll run needs its deletion approved — delete an unposted draft directly instead.");
        if (batch.PendingDeletion)
            throw new InvalidOperationException("A deletion request is already pending for this payroll run.");

        batch.PendingDeletion = true;
        batch.RequestedDeletionByEmployeeId = requestedByEmployeeId;
        await _payrollBatchService.UpdateAsync(batch, token, commit: false);
        await _approvalEngine.StartAsync(ApprovalApplicationType.PayrollPostingDeletion, batch.Id, requestedByEmployeeId, token);
        await _payrollBatchService.CommitChangesAsync(token);
    }

    public async Task ApproveDeletionAsync(Guid payrollBatchId, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var batch = await _payrollBatchService.FineOneAsync(payrollBatchId, token)
            ?? throw new NotFoundException("Payroll batch not found.");
        if (!batch.PendingDeletion)
            throw new InvalidOperationException("This payroll run has no deletion request awaiting approval.");
        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

        try
        {
            var result = await _approvalEngine.RecordActionAsync(
                ApprovalApplicationType.PayrollPostingDeletion, batch.Id,
                batch.RequestedDeletionByEmployeeId ?? batch.GeneratedByEmployeeId,
                approverId, approverHasOverride, ApprovalActionType.Approved, note, token);

            if (result.InstanceStatus == ApprovalInstanceStatus.Approved)
            {
                // Fully approved -- the batch (and its Payroll/ledger children) is actually
                // deleted now, bypassing DeleteBatchAsync's own IsPosted guard on purpose: that
                // guard exists to stop a direct delete of a posted batch, which is exactly what
                // this approved deletion request is meant to finally do.
                await ExecuteBatchDeletionAsync(batch, token);
            }
            await _payrollBatchService.CommitChangesAsync(token);
        }
        catch
        {
            if (_uow.CurrentTransaction != null)
            {
                await _uow.CurrentTransaction.RollbackAsync(token);
            }
            throw;
        }
    }

    public async Task DeclineDeletionAsync(Guid payrollBatchId, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token)
    {
        var batch = await _payrollBatchService.FineOneAsync(payrollBatchId, token)
            ?? throw new NotFoundException("Payroll batch not found.");
        if (!batch.PendingDeletion)
            throw new InvalidOperationException("This payroll run has no deletion request awaiting approval.");
        var approverId = approverEmployeeId ?? throw new InvalidOperationException(
            "Your account isn't linked to an Employee record, so this approval action can't be recorded. Contact an admin to link your account.");

        try
        {
            await _approvalEngine.RecordActionAsync(
                ApprovalApplicationType.PayrollPostingDeletion, batch.Id,
                batch.RequestedDeletionByEmployeeId ?? batch.GeneratedByEmployeeId,
                approverId, approverHasOverride, ApprovalActionType.Declined, note, token);

            // Declining a deletion request just reverts the batch to normal — ApprovalStatus was
            // never touched, so it's already back to Approved/posted as if nothing happened.
            batch.PendingDeletion = false;
            await _payrollBatchService.UpdateAsync(batch, token, commit: false);
            await _payrollBatchService.CommitChangesAsync(token);
        }
        catch
        {
            if (_uow.CurrentTransaction != null)
            {
                await _uow.CurrentTransaction.RollbackAsync(token);
            }
            throw;
        }
    }
}
