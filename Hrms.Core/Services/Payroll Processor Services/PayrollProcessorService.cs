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
        // Only approved (posted) DTR days that no payroll has paid yet are loaded -- see
        // DailyRecordService.PayrollRunRecordsAsync -- so a day is never paid twice, and DTR still
        // awaiting approval simply isn't payable yet.
        var (lines, records) = await CalculateRunAsync(payload, batch, token);
        if (lines.Count == 0)
            throw new ValidationException(
                "None of the selected employees has approved, unpaid DTR in this period.");

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
            // Left empty: which days this run paid is stamped on the DTR rows themselves
            // (DailyRecord.PayrollBatchId). DtrBatchCodes now only identifies legacy runs, which
            // paid whole DTR batches.
            DtrBatchCodes = null,
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
        // The days of the employees this run paid. Same transaction, so a failed save leaves
        // them unpaid; fails outright if another run paid any of them meanwhile.
        var paidEmployees = lines.Select(x => new EmployeeKey(x.EmployeeId)).ToHashSet();
        var paidRecordIds = records
            .Where(kv => paidEmployees.Contains(kv.Key))
            .SelectMany(kv => kv.Value.Select(r => r.Id))
            .ToList();
        await _dtrServie.MarkPaidAsync(batchHeader.Id, paidRecordIds, token);
        // Atomic commit point: if anything above failed (mapping error, cancellation), neither
        // the batch header nor any Payroll row is ever persisted — see AddAsync's doc comment.
        await _payrollService.CommitChangesAsync(token);
        await _statutoryLedgerService.SaveAsync(lines, token);
        // Stamps ConsumedByPayrollId on whatever SalaryAdjustment/OtherIncomeSchedules rows
        // this run's own EmployeePayrollLineService.Calculate already matched by date range
        // moments earlier — see PayrollInputConsumptionService.
        await _consumptionService.MarkConsumedByDateRangeAsync(payrolls, token);

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

    // The employee picker of the payroll run screen -- see DailyRecordService.GetPayrollRunCandidatesAsync.
    public Task<List<PayrollRunCandidate>> GetRunCandidatesAsync(PayrollRunCandidateQuery query, CancellationToken token)
    {
        if (query.FromDate == default || query.ToDate == default || query.FromDate > query.ToDate)
            throw new ValidationException("Choose a valid pay period.");
        return _dtrServie.GetPayrollRunCandidatesAsync(query, token);
    }

    public async Task<List<PayrollSummaryLine>> CalculateAsync(PayrollRunPayload payload, Guid batch, CancellationToken token) =>
        (await CalculateRunAsync(payload, batch, token)).Lines;

    private static void ValidateRunPayload(PayrollRunPayload payload)
    {
        if (payload.EmployeeIds.Count == 0)
            throw new ValidationException("Select at least one employee to pay.");
        if (payload.FromDate == default || payload.ToDate == default || payload.FromDate > payload.ToDate)
            throw new ValidationException("Choose a valid pay period.");
    }

    // The lines plus the DTR days they were computed from -- GenerateAsync stamps those days paid.
    private async Task<(List<PayrollSummaryLine> Lines, Dictionary<EmployeeKey, List<DailyRecordRunModel>> Records)>
        CalculateRunAsync(PayrollRunPayload payload, Guid batch, CancellationToken token)
    {
        ValidateRunPayload(payload);
        var payrollLines = new List<PayrollSummaryLine>();
        var records = await _dtrServie.LoadForPayrollRunAsync(payload, token);
        if (records.Count == 0) return (payrollLines, records);
        var leaveInfoByEmployee = await _dtrServie.LoadLeaveInfoForPayrollRunAsync(payload, token);
        // The chosen pay period, not the span of the days found -- a period with a few missing
        // days is still that period (proration, fixed-salary divisor, statutory dates).
        var dateRange = new DateRangePayload(payload.FromDate, payload.ToDate);
        var period = BuildPayrollPeriod(dateRange);
        var dtrRecords = (Records: records, dateRange.FromDate, dateRange.ToDate);

        var employees = dtrRecords.Records.Values
            .SelectMany(x => x.Select(y => y.Employee))
            .DistinctBy(x => x.Id)
            .ToList();

        if (employees == null || employees.Count() == 0)
        {
            return (payrollLines, records);
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
        return (payrollLines, records);
    }

    private static string BuildPayrollPeriod(DateRangePayload payload)
    {
        return string.Concat(
               payload.FromDate.ToString("MMM-dd-yyyy"), " ",
               payload.ToDate.ToString("MMM-dd-yyyy"),
               string.Empty);
    }
}
