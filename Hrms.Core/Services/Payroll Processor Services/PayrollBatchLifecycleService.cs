namespace Hrms.Core.Services;

// Post/Delete are run-level (whole PayrollBatch) transactions, never per-employee ones — an
// employee's payroll is never generated on its own, so it isn't posted or deleted on its own
// either. Extracted from PayrollProcessorService as its own lifecycle concern, independent of
// which flavor of payroll (regular/13th Month/Last Pay) produced the batch.
public class PayrollBatchLifecycleService
{
    private readonly PayrollBatchService _payrollBatchService;
    private readonly PayrollService _payrollService;
    private readonly SSSContributionService _sssContributionService;
    private readonly PHICContributionService _phicContributionService;
    private readonly HDMFContributionService _hdmfContributionService;
    private readonly TaxContributionService _taxContributionService;
    private readonly DailyRecordService _dtrServie;
    private readonly PayrollInputConsumptionService _consumptionService;

    public PayrollBatchLifecycleService(
        PayrollBatchService payrollBatchService,
        PayrollService payrollService,
        SSSContributionService sssContributionService,
        PHICContributionService phicContributionService,
        HDMFContributionService hdmfContributionService,
        TaxContributionService taxContributionService,
        DailyRecordService dtrServie,
        PayrollInputConsumptionService consumptionService)
    {
        _payrollBatchService = payrollBatchService;
        _payrollService = payrollService;
        _sssContributionService = sssContributionService;
        _phicContributionService = phicContributionService;
        _hdmfContributionService = hdmfContributionService;
        _taxContributionService = taxContributionService;
        _dtrServie = dtrServie;
        _consumptionService = consumptionService;
    }

    // Locks a whole Generate run in as final — an employee's payroll is never posted on its
    // own, since it was never generated on its own either. Updates the canonical
    // PayrollBatch.IsPosted plus each child Payroll row's denormalized copy (see
    // Payroll.PayrollBatchId doc comment) so existing per-row report filters keep working
    // unchanged.
    public async Task PostBatchAsync(Guid payrollBatchId, CancellationToken token)
    {
        await _payrollBatchService.PostAsync(payrollBatchId, token);
        await _payrollService.PostBatchAsync(payrollBatchId, token);
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
    public async Task DeleteBatchAsync(Guid payrollBatchId, CancellationToken token)
    {
        var batch = await _payrollBatchService.FineOneAsync(payrollBatchId, token);
        if (batch == null) throw new ValidationException("Payroll batch not found.");
        if (batch.IsPosted)
            throw new ValidationException("This payroll run has already been posted and can no longer be deleted.");

        // Captured before deletion — releases whatever SalaryAdjustment/OtherIncomeSchedules
        // rows this batch's own Payroll rows had claimed (see PayrollInputConsumptionService),
        // the same "undo what this run reserved" logic already applied below to DTR posting.
        var payrollIds = (await _payrollService.GetByBatchIdAsync(payrollBatchId, token))
            .Select(x => x.Id).ToList();

        await _sssContributionService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _phicContributionService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _hdmfContributionService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _taxContributionService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _payrollService.DeleteByBatchIdAsync(payrollBatchId, token);
        await _payrollService.CommitChangesAsync(token);
        await _consumptionService.ReleaseAsync(payrollIds, token);

        // Deleting a draft releases the DTR it was built from — unposts every DTR batch this
        // run used (idempotent/no-op on an already-unposted batch), the inverse of
        // PayrollProcessorService.GenerateAsync's post-on-save. Only ever reached for a draft
        // (IsPosted == false was already checked above), matching "unpost DTR if the draft is
        // deleted."
        var dtrBatchCodes = (batch.DtrBatchCodes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var batchCode in dtrBatchCodes)
        {
            await _dtrServie.UnpostAsync(batchCode, token);
        }

        await _payrollBatchService.DeleteAsync(payrollBatchId, token);
        await _payrollBatchService.CommitChangesAsync(token);
    }
}
