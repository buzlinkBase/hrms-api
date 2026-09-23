using DTR.Core;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// Thin orchestration Facade over the regular-payroll run lifecycle (Calculate -> Generate ->
// Post/Delete). 13th Month Pay, Last Pay, batch lifecycle, per-employee line calculation, and
// statutory ledger writing each live in their own focused class (see EmployeePayrollLineService,
// ThirteenthMonthPayrollService, LastPayrollService, PayrollBatchLifecycleService,
// StatutoryContributionLedgerService) — this class only orchestrates them, matching
// PayrollsController's public API 1:1 so no consumer needed to change when it was split out of
// what used to be one 1200-line class.
public class PayrollProcessorService
{
    private readonly DailyRecordService _dtrServie;
    private readonly PayrollRangeContextComposerService _payloadComposer;
    private readonly PayrollService _payrollService;
    private readonly IMapper _mapper;
    private readonly EmployeePayrollInclusionResolver _inclusionResolver;
    private readonly LeaveService _leaveService;
    private readonly PayrollBatchService _payrollBatchService;
    private readonly EmployeePayrollLineService _lineService;
    private readonly ThirteenthMonthPayrollService _thirteenthMonthPayrollService;
    private readonly LastPayrollService _lastPayrollService;
    private readonly TaxAnnualizationService _taxAnnualizationService;
    private readonly PayrollBatchLifecycleService _batchLifecycleService;
    private readonly StatutoryContributionLedgerService _statutoryLedgerService;
    private readonly PayrollInputConsumptionService _consumptionService;
    private readonly YearLockService _yearLockService;
    private readonly ApprovalEngineService _approvalEngine;
    private readonly DTRBatchService _dtrBatchService;

    public PayrollProcessorService(
        PayrollRangeContextComposerService payloadComposer,
        DailyRecordService dtrServie,
        PayrollService payrollService,
        IMapper mapper,
        EmployeePayrollInclusionResolver inclusionResolver,
        LeaveService leaveService,
        PayrollBatchService payrollBatchService,
        EmployeePayrollLineService lineService,
        ThirteenthMonthPayrollService thirteenthMonthPayrollService,
        LastPayrollService lastPayrollService,
        TaxAnnualizationService taxAnnualizationService,
        PayrollBatchLifecycleService batchLifecycleService,
        StatutoryContributionLedgerService statutoryLedgerService,
        PayrollInputConsumptionService consumptionService,
        YearLockService yearLockService,
        ApprovalEngineService approvalEngine,
        DTRBatchService dtrBatchService)
    {
        _dtrServie = dtrServie;
        _payloadComposer = payloadComposer;
        _payrollService = payrollService;
        _mapper = mapper;
        _inclusionResolver = inclusionResolver;
        _leaveService = leaveService;
        _payrollBatchService = payrollBatchService;
        _lineService = lineService;
        _thirteenthMonthPayrollService = thirteenthMonthPayrollService;
        _lastPayrollService = lastPayrollService;
        _taxAnnualizationService = taxAnnualizationService;
        _batchLifecycleService = batchLifecycleService;
        _statutoryLedgerService = statutoryLedgerService;
        _consumptionService = consumptionService;
        _yearLockService = yearLockService;
        _approvalEngine = approvalEngine;
        _dtrBatchService = dtrBatchService;
    }

    public async Task<List<PayrollSummaryLine>> GenerateAsync(PayrollRunPayload payload, Guid batch, Guid generatedByEmployeeId, CancellationToken token)
    {
        var usedBatchCodes = await _payrollBatchService.GetUsedDtrBatchCodesAsync(token);
        var alreadyPosted = payload.BatchCodes.Where(usedBatchCodes.Contains).ToList();
        if (alreadyPosted.Count > 0)
        {
            throw new ValidationException(
                $"Payroll has already been generated for DTR batch(es): {string.Join(", ", alreadyPosted)}. " +
                "Delete the existing payroll run first if you need to regenerate it.");
        }

        // Blocks generating payroll from a DTR batch that hasn't cleared its own approval yet --
        // a batch with no DTRBatch row at all (legacy, predates this entity) is treated as
        // already-Approved, same convention as DailyRecordService.GetBatches.
        var dtrBatches = await _dtrBatchService.FindByCodesAsync(payload.BatchCodes, token);
        var unapprovedDtrBatches = dtrBatches
            .Where(b => b.ApprovalStatus != ApprovalStatus.Approved)
            .Select(b => b.BatchCode)
            .ToList();
        if (unapprovedDtrBatches.Count > 0)
        {
            throw new ValidationException(
                $"Payroll generation is blocked — the following DTR batch(es) are not yet approved: " +
                $"{string.Join(", ", unapprovedDtrBatches)}.");
        }

        var lines = await CalculateAsync(payload, batch,token);
        if (lines.Count() == 0) return lines;

        var touchedYears = lines.Select(x => x.PostingPeriod.Year).Distinct().ToList();
        foreach (var year in touchedYears)
        {
            if (await _yearLockService.IsYearLockedAsync(year, token))
            {
                throw new ValidationException(
                    $"Payroll for {year} is locked — the Year-End Tax Adjustment has already been posted for " +
                    "this year. Reopen the year first if changes are required.");
            }
        }

        var savingBatch = batch.ToString();

        // CalculateAsync already stamped every line's PayrollBatchId with the id it wants to
        // travel with (see EmployeePayrollLineService.Calculate -> InitializePayrollLine) — the
        // PayrollBatch header row must be inserted under that same id, otherwise
        // Payroll.PayrollBatchId (what the frontend groups by and posts/deletes against) never
        // matches any real PayrollBatch.Id.
        var batchHeader = new PayrollBatch
        {
            Id = lines.First().PayrollBatchId,
            PayPeriodStart = lines.MinBy(x => x.PayPeriodStart)!.PayPeriodStart,
            PayPeriodEnd = lines.MaxBy(x => x.PayPeriodEnd)!.PayPeriodEnd,
            PayDate = payload.PayDate,
            DtrBatchCodes = string.Join(",", payload.BatchCodes),
            Remarks = payload.Remarks,
            GeneratedByEmployeeId = generatedByEmployeeId,
        };
        await _payrollBatchService.AddAsync(batchHeader, token, commit: false);
        // Starts the PayrollPosting approval instance the moment this draft is Generated/Saved,
        // same as every other application type -- see PayrollBatchLifecycleService.
        // ApproveBatchAsync/DeclineBatchAsync, which act on it once submitted.
        await _approvalEngine.StartAsync(ApprovalApplicationType.PayrollPosting, batchHeader.Id, generatedByEmployeeId, token);

        // Generating no longer marks payroll as posted — a run stays an editable/deletable
        // draft (PayrollBatch.IsPosted defaults to false) until explicitly posted via
        // ApproveBatchAsync. Saving is not posting.
        var payrolls = _mapper.Map<List<Payroll>>(lines);
        foreach (var payroll in payrolls)
        {
            payroll.BatchCode = savingBatch;
        }
        await _payrollService.SavePayrollsAsync(payrolls, token, commit: false);
        // Atomic commit point: if anything above failed (mapping error, cancellation), neither
        // the batch header nor any Payroll row is ever persisted — see AddAsync's doc comment.
        await _payrollService.CommitChangesAsync(token);
        await _statutoryLedgerService.SaveAsync(lines, token);
        // Stamps ConsumedByPayrollId on whatever SalaryAdjustment/OtherIncomeSchedules rows
        // this run's own EmployeePayrollLineService.Calculate already matched by date range
        // moments earlier — see PayrollInputConsumptionService.
        await _consumptionService.MarkConsumedByDateRangeAsync(payrolls, token);

        // No DTR posting loop here anymore -- the unapproved-DTR-batch check above already
        // guarantees every batch this run used is Approved, which (via DailyRecordService.
        // ApproveBatchAsync) already means every one of its DailyRecord rows is Posted. Calling
        // PostAsync again here would be a redundant no-op.
        return lines;
    }

    public Task<List<PayrollSummaryLine>> GenerateThirteenthMonthAsync(ThirteenthMonthRunPayload payload, Guid generatedByEmployeeId, CancellationToken token) =>
        _thirteenthMonthPayrollService.GenerateAsync(payload, generatedByEmployeeId, token);

    public Task<List<PayrollSummaryLine>> GenerateLastPayAsync(LastPayRunPayload payload, Guid generatedByEmployeeId, CancellationToken token) =>
        _lastPayrollService.GenerateAsync(payload, generatedByEmployeeId, token);

    public Task<List<TaxAnnualizationPreviewModel>> PreviewYearEndAdjustmentAsync(TaxAnnualizationRunPayload payload, CancellationToken token) =>
        _taxAnnualizationService.PreviewAsync(payload, token);

    public Task<List<PayrollSummaryLine>> GenerateYearEndAdjustmentAsync(TaxAnnualizationRunPayload payload, Guid generatedByEmployeeId, CancellationToken token) =>
        _taxAnnualizationService.GenerateAsync(payload, generatedByEmployeeId, token);

    // Review-step data for the Last Pay generation screen — see LastPayrollService's own
    // doc comments on these two.
    public Task<List<SalaryAdjustment>> GetAvailableSalaryAdjustmentsAsync(List<Guid> employeeIds, CancellationToken token) =>
        _lastPayrollService.GetAvailableSalaryAdjustmentsAsync(employeeIds, token);

    public Task<List<OtherIncomeSchedules>> GetAvailableOtherIncomeAsync(List<Guid> employeeIds, CancellationToken token) =>
        _lastPayrollService.GetAvailableOtherIncomeAsync(employeeIds, token);

    public Task<List<LastPayAttendanceWarning>> GetLastPayAttendanceWarningsAsync(List<Guid> employeeIds, CancellationToken token) =>
        _lastPayrollService.GetAttendanceWarningsAsync(employeeIds, token);

    public Task<List<CashBondReportModel>> GetCashBondStatusAsync(List<Guid> employeeIds, CancellationToken token) =>
        _lastPayrollService.GetCashBondStatusAsync(employeeIds, token);

    public Task ApproveBatchAsync(Guid payrollBatchId, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token) =>
        _batchLifecycleService.ApproveBatchAsync(payrollBatchId, approverEmployeeId, approverHasOverride, note, token);

    public Task DeclineBatchAsync(Guid payrollBatchId, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token) =>
        _batchLifecycleService.DeclineBatchAsync(payrollBatchId, approverEmployeeId, approverHasOverride, note, token);

    public Task DeleteBatchAsync(Guid payrollBatchId, CancellationToken token) =>
        _batchLifecycleService.DeleteBatchAsync(payrollBatchId, token);

    public Task RequestDeletionAsync(Guid payrollBatchId, Guid requestedByEmployeeId, CancellationToken token) =>
        _batchLifecycleService.RequestDeletionAsync(payrollBatchId, requestedByEmployeeId, token);

    public Task ApproveDeletionAsync(Guid payrollBatchId, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token) =>
        _batchLifecycleService.ApproveDeletionAsync(payrollBatchId, approverEmployeeId, approverHasOverride, note, token);

    public Task DeclineDeletionAsync(Guid payrollBatchId, Guid? approverEmployeeId, bool approverHasOverride, string? note, CancellationToken token) =>
        _batchLifecycleService.DeclineDeletionAsync(payrollBatchId, approverEmployeeId, approverHasOverride, note, token);

    // Batch-list projection for the Payroll Batches tab -- see PayrollBatchService.GetBatchesAsync.
    public Task<List<PayrollBatchListModel>> GetBatchesAsync(DateOnly from, DateOnly to, CancellationToken token) =>
        _payrollBatchService.GetBatchesAsync(from, to, token);

    public async Task<List<PayrollSummaryLine>> CalculateAsync(PayrollRunPayload payload,Guid batch, CancellationToken token)
    {
        var payrollLines = new List<PayrollSummaryLine>();
        var dtrRecords = await _dtrServie.LoadForPayrollRunAsync(payload.BatchCodes, token);
        if (dtrRecords.Records == null || !dtrRecords.Records.Any()) return payrollLines;
        var leaveInfoByEmployee = await _dtrServie.LoadLeaveInfoForPayrollRunAsync(payload.BatchCodes, token);
        var dateRange = new DateRangePayload(dtrRecords.FromDate, dtrRecords.ToDate);
        var period = BuildPayrollPeriod(dateRange);

        var employees = dtrRecords.Records.Values
            .SelectMany(x => x.Select(y => y.Employee))
            .DistinctBy(x => x.Id)
            .ToList();

        if (employees == null || employees.Count() == 0)
        {
            return payrollLines;
        }

        // Resolve tenant-vs-employee Fixed-salary inclusion settings once, upstream —
        // every downstream DTR pay policy keeps reading employee.IsXxxIncluded unchanged.
        await _inclusionResolver.ApplyAsync(employees!, token);
        var rangePayload = await _payloadComposer
                         .ComposePayload(dateRange, employees, token, payload.PayDate)
                          ?? throw new Exception("Unable to load range payload");

        // PayDate is user-supplied input, not a config fallback — silently defaulting to
        // ToDate here would quietly violate the company's chosen posting policy, so this
        // fails loudly instead (StatutoryCreditDateResolver's own fallback is defense in
        // depth only).
        var needsPayDate = rangePayload.CompanyPolicy.CrossMonthStatutoryCreditPolicy == CrossMonthStatutoryCreditPolicy.PayDate
            || rangePayload.CompanyPolicy.WTaxCrossMonthCreditPolicy == CrossMonthStatutoryCreditPolicy.PayDate;
        if (needsPayDate && payload.PayDate == null)
            throw new ValidationException("A Pay/Release Date is required to generate this payroll under the configured statutory posting policy.");

        // Small master table — load once per run rather than per employee. Used to split
        // PaidLeaves by funding source (see EmployeePayrollLineService).
        var leavePaySourceMap = (await _leaveService.FindAllAsync(token))
            .ToDictionary(x => x.Id, x => x.PaySource);

        foreach (var employee in employees)
        {
            if (employee == null) continue;
            if (!dtrRecords.Records.TryGetValue(new EmployeeKey(employee.Id), out var empDtr))
            {
                continue;
            }
            leaveInfoByEmployee.TryGetValue(new EmployeeKey(employee.Id), out var empLeaveInfo);
            var payrollLine = _lineService.Calculate(
                dateRange, empDtr, employee, rangePayload, batch, period,
                rangePayload.CompanyPolicy.CrossMonthStatutoryCreditPolicy,
                rangePayload.CompanyPolicy.WTaxCrossMonthCreditPolicy,
                payload.PayDate, payload.Remarks, empLeaveInfo, leavePaySourceMap);
            payrollLines.Add(payrollLine);
        }
        return payrollLines;
    }

    private static string BuildPayrollPeriod(DateRangePayload payload)
    {
        return string.Concat(
               payload.FromDate.ToString("MMM-dd-yyyy"), " ",
               payload.ToDate.ToString("MMM-dd-yyyy"),
               string.Empty);
    }
}
