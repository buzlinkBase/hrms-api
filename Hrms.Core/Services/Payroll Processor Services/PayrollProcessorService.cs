using DTR.Core;
using Hrms.Core.Policies.DeductionPolicies;
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
    private readonly EmployeeService _employeeService;
    private readonly PayrollDeductionClaimService _claimService;

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
        DTRBatchService dtrBatchService,
        EmployeeService employeeService,
        PayrollDeductionClaimService claimService)
    {
        _claimService = claimService;
        _employeeService = employeeService;
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
        ValidatePayload(payload);
        // Legacy batch mode only -- cutoff mode pays by day instead (Posted == approved, and
        // PayrollBatchId == null == not paid yet), so there are no whole batches to check.
        if (!payload.IsCutoffMode)
        {
            var batchCodes = payload.BatchCodes!;
            var usedBatchCodes = await _payrollBatchService.GetUsedDtrBatchCodesAsync(token);
            var alreadyPosted = batchCodes.Where(usedBatchCodes.Contains).ToList();
            if (alreadyPosted.Count > 0)
            {
                throw new ValidationException(
                    $"Payroll has already been generated for DTR batch(es): {string.Join(", ", alreadyPosted)}. " +
                    "Delete the existing payroll run first if you need to regenerate it.");
            }

            // Blocks generating payroll from a DTR batch that hasn't cleared its own approval yet --
            // a batch with no DTRBatch row at all (legacy, predates this entity) is treated as
            // already-Approved, same convention as DailyRecordService.GetBatches.
            var dtrBatches = await _dtrBatchService.FindByCodesAsync(batchCodes, token);
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
        }

        var (lines, paidDayIds, dtrBatchCodes, claims) = await CalculateCoreAsync(payload, batch, token);
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
            DtrBatchCodes = string.Join(",", dtrBatchCodes),
            Remarks = payload.Remarks,
            GeneratedByEmployeeId = generatedByEmployeeId,
        };
        await _payrollBatchService.AddAsync(batchHeader, token, commit: false);
        // Starts the PayrollPosting approval instance the moment this draft is Generated/Saved,
        // same as every other application type -- see PayrollBatchLifecycleService.
        // ApproveBatchAsync/DeclineBatchAsync, which act on it once submitted.
        await _approvalEngine.StartAsync(ApprovalApplicationType.PayrollPosting, batchHeader.Id, generatedByEmployeeId, token,
            scope: ApprovalScopeContext.ForPayrollGroups(lines.Select(l => l.PayrollGroupId)));

        // Generating no longer marks payroll as posted — a run stays an editable/deletable
        // draft (PayrollBatch.IsPosted defaults to false) until explicitly posted via
        // ApproveBatchAsync. Saving is not posting.
        var payrolls = _mapper.Map<List<Payroll>>(lines);
        foreach (var payroll in payrolls)
        {
            payroll.BatchCode = savingBatch;
        }
        await _payrollService.SavePayrollsAsync(payrolls, token, commit: false);
        // Marks every DTR day this run paid, in the same unit of work as the header and Payroll
        // rows above -- a day can never end up paid without its run, or in two runs.
        await _dtrServie.MarkPaidAsync(paidDayIds, batchHeader.Id, token);
        // Takes the deduction period for every employee this run carries, in the same commit as the
        // run. PayrollDeductionClaim's unique (employee, period) index makes a second run that
        // slipped past the preview's check fail here instead of deducting twice.
        _claimService.Stage(batchHeader.Id, claims);
        // Atomic commit point: if anything above failed (mapping error, cancellation), neither
        // the batch header nor any Payroll row is ever persisted — see AddAsync's doc comment.
        try
        {
            await _payrollService.CommitChangesAsync(token);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (claims.Count > 0 && IsUniqueViolation(ex))
        {
            throw new ValidationException(
                "Another payroll run just took the deductions for some of these employees. Preview again and retry.");
        }
        await _statutoryLedgerService.SaveAsync(lines, token);
        // Stamps ConsumedByPayrollId on whatever SalaryAdjustment/OtherIncomeSchedules rows
        // this run's own EmployeePayrollLineService.Calculate already matched by date range
        // moments earlier — see PayrollInputConsumptionService. Earnings-only rows matched none
        // (their deduction inputs were never loaded) and must not sweep the carrier run's rows.
        await _consumptionService.MarkConsumedByDateRangeAsync(payrolls.Where(p => p.IsDeductionCarrier).ToList(), token);

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
        ValidatePayload(payload);
        return (await CalculateCoreAsync(payload, batch, token)).Lines;
    }

    // Cutoff-mode run's "not ready" warning -- see DailyRecordService.GetNotReadyEmployeesAsync.
    public Task<List<PayrollNotReadyEmployee>> GetNotReadyEmployeesAsync(PayrollRunPayload payload, CancellationToken token)
    {
        if (!payload.IsCutoffMode) return Task.FromResult(new List<PayrollNotReadyEmployee>());
        ValidatePayload(payload);
        return GetCutoffNotReadyAsync(payload, token);
    }

    private async Task<List<PayrollNotReadyEmployee>> GetCutoffNotReadyAsync(PayrollRunPayload payload, CancellationToken token)
    {
        var carrierMode = await _payloadComposer.GetDeductionCarrierModeAsync();
        var scope = payload.ToCutoffScope(useDayClient: carrierMode != DeductionCarrierMode.EveryRun);
        var result = await _dtrServie.GetNotReadyEmployeesAsync(scope, token);

        // FIXED-salary employees the run will skip because an earlier run already paid their
        // period salary -- see CalculateCoreAsync. Their approved days stay unpaid until that run
        // is deleted, so the user needs to see who was left out.
        var fixedCandidates = await _dtrServie.GetPayableFixedSalaryEmployeesAsync(scope, token);
        if (fixedCandidates.Count > 0)
        {
            var alreadyPaid = await _payrollService.GetEmployeeIdsWithRegularPayrollInRangeAsync(
                fixedCandidates.Select(x => x.EmployeeId).ToList(), scope.From, scope.To, token);
            foreach (var e in fixedCandidates.Where(x => alreadyPaid.Contains(x.EmployeeId)))
            {
                e.Reason = "FixedSalaryAlreadyPaid";
                result.Add(e);
            }
        }
        return result;
    }

    // Who in the range can be paid right now -- the Payroll Run screen's readiness list, shown
    // before anything is previewed. Uses the same day rules as a cutoff-mode run (approved and
    // unpaid days are paid; FIXED employees whose period pay is already in a Regular run are
    // skipped), so "Ready" here means a run over the same range would include them.
    // Cutoff Tracker: who worked for which client in the window, and which run carries each
    // employee's deductions. Three set-based queries however many employees there are. Empty when
    // the company doesn't gate deductions (EveryRun), since there are then no carriers to track.
    public async Task<CutoffTrackerResult> GetCutoffTrackerAsync(
        DateOnly from, DateOnly to, Guid? payrollGroupId, CancellationToken token)
    {
        if (from > to)
            throw new ValidationException("The date range's start date is after its end date.");
        var mode = await _payloadComposer.GetDeductionCarrierModeAsync();
        var result = new CutoffTrackerResult { Mode = mode.ToString() };
        if (mode == DeductionCarrierMode.EveryRun) return result;

        result.Employees = await _dtrServie.GetCutoffTrackerRowsAsync(from, to, payrollGroupId, token);
        if (result.Employees.Count == 0) return result;

        var claims = (await _claimService.FindOverlappingAsync(from, to, token))
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.PeriodStart).First());
        var carrierRows = await _payrollService.GetCarrierRowsAsync(
            claims.Values.Select(x => x.PayrollBatchId).Distinct().ToList(), token);

        foreach (var employee in result.Employees)
        {
            if (!claims.TryGetValue(employee.EmployeeId, out var claim)) continue;
            carrierRows.TryGetValue((employee.EmployeeId, claim.PayrollBatchId), out var row);
            employee.Carrier = new CutoffTrackerCarrier
            {
                PayrollBatchId = claim.PayrollBatchId,
                ClientId = row.ClientId,
                PeriodStart = claim.PeriodStart,
                PeriodEnd = claim.PeriodEnd,
                PayDate = row.PayDate,
                Remarks = row.Remarks,
            };
        }
        return result;
    }

    public Task<CutoffStatementModel> GetCutoffStatementAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken token)
    {
        if (from > to)
            throw new ValidationException("The date range's start date is after its end date.");
        return _payrollService.GetCutoffStatementAsync(employeeId, from, to, token);
    }

    // Whether runs pick their days by the client each day was worked for (a deduction-carrier
    // company) -- the Payroll Run screen needs it to know which client tag to narrow readiness by.
    public async Task<bool> UsesDayClientAsync() =>
        await _payloadComposer.GetDeductionCarrierModeAsync() != DeductionCarrierMode.EveryRun;

    public async Task<List<PayrollReadinessEmployee>> GetReadinessAsync(
        DateOnly from, DateOnly to, Guid? payrollGroupId, CancellationToken token,
        IReadOnlyCollection<Guid>? clientIds = null)
    {
        if (from > to)
            throw new ValidationException("The date range's start date is after its end date.");
        var result = await _dtrServie.GetPayrollReadinessAsync(
            from, to, payrollGroupId, token, clientIds, useDayClient: await UsesDayClientAsync());

        var fixedWithApproved = result
            .Where(x => x.SalaryType == SalaryType.FIXED && x.ApprovedDays > 0)
            .Select(x => x.EmployeeId)
            .ToList();
        var fixedAlreadyPaid = await _payrollService.GetEmployeeIdsWithRegularPayrollInRangeAsync(fixedWithApproved, from, to, token);

        foreach (var e in result)
        {
            e.Status = fixedAlreadyPaid.Contains(e.EmployeeId) ? "FixedSalaryAlreadyPaid"
                : e.ApprovedDays > 0 && e.PendingDays > 0 ? "Partial"
                : e.ApprovedDays > 0 ? "Ready"
                : e.PendingDays > 0 ? "Pending"
                : "Paid";
        }

        // Payroll-only (no DTR) employees have no DTR days, so the DTR-based list never contains
        // them; a cutoff run does pay them, so show them as Ready (or already paid).
        var payrollOnly = (await _employeeService.GetNoDtrForPayrollRunAsync(new CutoffScope(from, to, payrollGroupId), token))
            .Where(x => result.All(r => r.EmployeeId != x.Id))
            .ToList();
        if (payrollOnly.Count > 0)
        {
            var paid = await _payrollService.GetEmployeeIdsWithRegularPayrollInRangeAsync(
                payrollOnly.Select(x => x.Id).ToList(), from, to, token);
            result.AddRange(payrollOnly.Select(x => new PayrollReadinessEmployee
            {
                EmployeeId = x.Id,
                EmployeeNo = x.EmployeeNo,
                FullName = x.FullName,
                PayrollGroupId = x.PayrollGroupId,
                ClientId = x.ClientId,
                SalaryType = x.SalaryType,
                IsPayrollOnly = true,
                Status = paid.Contains(x.Id) ? "FixedSalaryAlreadyPaid" : "Ready",
            }));
        }
        return result;
    }

    private static void ValidatePayload(PayrollRunPayload payload)
    {
        if (!payload.IsCutoffMode) return;
        if (payload.FromDate == null || payload.ToDate == null)
            throw new ValidationException("Select a date range, or select DTR batches, to run payroll.");
        if (payload.FromDate > payload.ToDate)
            throw new ValidationException("The date range's start date is after its end date.");
    }

    // Lines plus what Generate needs to persist alongside them: the ids of the DTR days that were
    // paid (stamped with the run's id) and the distinct DTR batch codes they came from.
    private async Task<(List<PayrollSummaryLine> Lines, List<Guid> PaidDayIds, List<string> DtrBatchCodes, List<(Guid EmployeeId, DeductionPeriod Period)> Claims)>
        CalculateCoreAsync(PayrollRunPayload payload, Guid batch, CancellationToken token)
    {
        var payrollLines = new List<PayrollSummaryLine>();
        var noRun = (payrollLines, new List<Guid>(), new List<string>(), new List<(Guid EmployeeId, DeductionPeriod Period)>());
        // Cutoff mode only: the batch-code mode pays whole DTR batches, which carry no per-day client.
        var carrierMode = payload.IsCutoffMode ? await _payloadComposer.GetDeductionCarrierModeAsync() : DeductionCarrierMode.EveryRun;
        var gateDeductions = carrierMode != DeductionCarrierMode.EveryRun;
        var scope = payload.IsCutoffMode ? payload.ToCutoffScope(useDayClient: gateDeductions) : null;
        var dtrRecords = payload.IsCutoffMode
            ? await _dtrServie.LoadForPayrollRunByCutoffAsync(scope!, token)
            : await _dtrServie.LoadForPayrollRunAsync(payload.BatchCodes!, token);
        var records = dtrRecords.Records ?? [];

        // Payroll-only (no DTR) employees -- owners, shareholders, officers... -- are paid from their
        // fixed rate for the part of the cutoff their window covers, never from DTR days. They are
        // pulled out of the DTR-derived set (a flagged employee's leftover DTR rows are not paid) and
        // added through their own loader; only cutoff mode has a date range to test the window against.
        var payrollOnly = payload.IsCutoffMode
            ? await _employeeService.GetNoDtrForPayrollRunAsync(scope!, token)
            : [];
        var payrollOnlyIds = payrollOnly.Select(x => x.Id).ToHashSet();

        if (!records.Any() && payrollOnly.Count == 0) return noRun;
        var leaveInfoByEmployee = payload.IsCutoffMode
            ? await _dtrServie.LoadLeaveInfoForPayrollRunByCutoffAsync(scope!, token)
            : await _dtrServie.LoadLeaveInfoForPayrollRunAsync(payload.BatchCodes!, token);
        var dateRange = new DateRangePayload(dtrRecords.FromDate, dtrRecords.ToDate);
        var period = BuildPayrollPeriod(dateRange);

        var employees = records.Values
            .SelectMany(x => x.Select(y => y.Employee))
            .DistinctBy(x => x.Id)
            .Where(x => !payrollOnlyIds.Contains(x.Id))
            .ToList();
        employees.AddRange(payrollOnly);

        if (employees.Count == 0)
        {
            return noRun;
        }

        // A FIXED-salary employee's basic pay is the flat period rate, not the sum of the days in
        // this run, so a later run for a cutoff that already has a Regular payroll row would pay
        // the whole period salary again. Skip them (and leave their days unpaid and unstamped)
        // until the earlier run is deleted -- GetNotReadyEmployeesAsync tells the user who.
        if (payload.IsCutoffMode)
        {
            var fixedIds = employees.Where(e => e != null && e.SalaryType == SalaryType.FIXED).Select(e => e!.Id).ToList();
            if (fixedIds.Count > 0)
            {
                var alreadyPaid = await _payrollService.GetEmployeeIdsWithRegularPayrollInRangeAsync(
                    fixedIds, dateRange.FromDate, dateRange.ToDate, token);
                employees = employees.Where(e => e != null && !alreadyPaid.Contains(e.Id)).ToList();
                if (employees.Count == 0) return noRun;
            }
        }

        // Who takes each employee's deductions this period (statutory, tax, loans, general other
        // income). Under EveryRun everyone does, as before, and nothing below changes.
        var carrierDecisions = gateDeductions
            ? await ResolveCarriersAsync(carrierMode, payload, employees!, records, dateRange, token)
            : null;
        var claims = carrierDecisions == null
            ? new List<(Guid EmployeeId, DeductionPeriod Period)>()
            : carrierDecisions.Where(x => x.Value.Decision.Carries)
                .Select(x => (EmployeeId: x.Key, Period: x.Value.Period)).ToList();

        // Resolve tenant-vs-employee Fixed-salary inclusion settings once, upstream —
        // every downstream DTR pay policy keeps reading employee.IsXxxIncluded unchanged.
        await _inclusionResolver.ApplyAsync(employees!, token);
        // Earnings-only employees are left out of the deduction / other income / salary adjustment
        // loads altogether, so a reliever-heavy run does less work than a normal one.
        var rangePayload = await _payloadComposer
                         .ComposePayload(dateRange, employees, token, payload.PayDate,
                             carrierDecisions == null ? null : claims.Select(x => x.EmployeeId).ToList())
                          ?? throw new Exception("Unable to load range payload");

        // Pay/Release Date is optional: when the policy is PayDate and none is supplied,
        // StatutoryCreditDateResolver falls back to the cutoff end date.

        // Small master table — load once per run rather than per employee. Used to split
        // PaidLeaves by funding source (see EmployeePayrollLineService).
        var leavePaySourceMap = (await _leaveService.FindAllAsync(token))
            .ToDictionary(x => x.Id, x => x.PaySource);

        foreach (var employee in employees)
        {
            if (employee == null) continue;
            List<DailyRecordRunModel>? empDtr;
            if (payrollOnlyIds.Contains(employee.Id))
            {
                empDtr = [];
            }
            else if (!records.TryGetValue(new EmployeeKey(employee.Id), out empDtr))
            {
                continue;
            }
            leaveInfoByEmployee.TryGetValue(new EmployeeKey(employee.Id), out var empLeaveInfo);
            CarrierPlan? carrierPlan = null;
            carrierDecisions?.TryGetValue(employee.Id, out carrierPlan);
            var carrier = carrierPlan?.Decision;
            var otherUnpaidDays = carrierPlan?.OtherUnpaidDays ?? 0;
            var payrollLine = _lineService.Calculate(
                dateRange, empDtr, employee, rangePayload, batch, period,
                rangePayload.CompanyPolicy.CrossMonthStatutoryCreditPolicy,
                rangePayload.CompanyPolicy.WTaxCrossMonthCreditPolicy,
                payload.PayDate, payload.Remarks, empLeaveInfo, leavePaySourceMap,
                carriesDeductions: carrier?.Carries ?? true);
            if (carrier != null)
            {
                payrollLine.DeductionCarrierReason = carrier.Reason;
                payrollLine.OtherUnpaidDays = carrier.Carries ? otherUnpaidDays : 0;
                payrollLine.OtherClients = carrierPlan!.OtherClients;
                // A per-client run is attributed to the client whose days it pays, not the client on
                // the employee's own record, so reports by client see a reliever where they worked.
                var dayClient = empDtr
                    .Where(d => d.ClientId != null)
                    .GroupBy(d => d.ClientId)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault();
                if (dayClient != null) payrollLine.ClientId = dayClient;
            }
            payrollLines.Add(payrollLine);
        }

        // Only the days of employees this run actually paid (a skipped FIXED employee's stay unpaid).
        var allDays = employees
            .Where(e => !payrollOnlyIds.Contains(e!.Id))
            .SelectMany(e => records.TryGetValue(new EmployeeKey(e!.Id), out var days) ? days : new List<DailyRecordRunModel>())
            .ToList();
        var paidDayIds = allDays.Select(x => x.Id).ToList();
        var dtrBatchCodes = allDays
            .Select(x => x.BatchCode)
            .Where(c => !string.IsNullOrEmpty(c))
            .Select(c => c!)
            .Distinct()
            .Order()
            .ToList();
        return (payrollLines, paidDayIds, dtrBatchCodes, claims);
    }

    private sealed record CarrierPlan(
        DeductionCarrierDecision Decision, DeductionPeriod Period,
        List<OtherClientDays> OtherClients, int OtherUnpaidDays = 0);

    // Decides, for every employee in the run at once, whether this run carries their deductions.
    // Constant query count whatever the headcount: one claims lookup, and one grouped day count per
    // distinct deduction period (a company has a handful of cutoffs, not one per employee).
    private async Task<Dictionary<Guid, CarrierPlan>> ResolveCarriersAsync(
        DeductionCarrierMode mode,
        PayrollRunPayload payload,
        List<EmployeeModelPayrollRun> employees,
        Dictionary<EmployeeKey, List<DailyRecordRunModel>> records,
        DateRangePayload dateRange,
        CancellationToken token)
    {
        var periods = employees.ToDictionary(e => e.Id, e => DeductionPeriodResolver.Resolve(e.PayrollGroup, dateRange.ToDate));
        var claimed = await _claimService.FindClaimsAsync(
            periods.Keys.ToList(), periods.Values.Min(p => p.Start), periods.Values.Max(p => p.Start), token);

        // The whole period's worked days per client (what the ranking rules read, and what the
        // preview's "also worked at" chips show), and the part of them no run has paid yet. Two
        // grouped queries per distinct period.
        var daysByEmployee = new Dictionary<Guid, Dictionary<Guid, int>>();
        var unpaidByEmployee = new Dictionary<Guid, Dictionary<Guid, int>>();
        foreach (var group in periods.GroupBy(x => x.Value))
        {
            var ids = group.Select(x => x.Key).ToList();
            foreach (var (employeeId, byClient) in await _dtrServie.CountWorkedDaysByClientAsync(
                ids, group.Key.Start, group.Key.End, token))
                daysByEmployee[employeeId] = byClient;
            foreach (var (employeeId, byClient) in await _dtrServie.CountWorkedDaysByClientAsync(
                ids, group.Key.Start, group.Key.End, token, unpaidOnly: true))
                unpaidByEmployee[employeeId] = byClient;
        }

        var carry = payload.CarryDeductionEmployeeIds?.ToHashSet() ?? [];
        var skip = payload.SkipDeductionEmployeeIds?.ToHashSet() ?? [];
        var empty = new Dictionary<Guid, int>();
        var plans = new Dictionary<Guid, CarrierPlan>();
        foreach (var employee in employees)
        {
            var period = periods[employee.Id];
            IReadOnlySet<Guid> inThisRun = records.TryGetValue(new EmployeeKey(employee.Id), out var days)
                ? days.Select(d => d.ClientId ?? Guid.Empty).ToHashSet()
                : new HashSet<Guid>();
            var input = new DeductionCarrierInput(
                employee.Id,
                employee.ClientId,
                inThisRun,
                daysByEmployee.GetValueOrDefault(employee.Id) ?? empty,
                claimed.TryGetValue((employee.Id, period.Start), out var holder) ? holder : null,
                carry.Contains(employee.Id),
                skip.Contains(employee.Id));
            var decision = DeductionCarrierResolver.Decide(mode, input);

            // The clients this employee worked in the period that this run does NOT pay, with how
            // many of those days no run has paid yet. A carrier run brackets contributions and tax
            // off what is paid so far, so unpaid days elsewhere are flagged on the preview.
            var unpaid = unpaidByEmployee.GetValueOrDefault(employee.Id) ?? empty;
            var others = (daysByEmployee.GetValueOrDefault(employee.Id) ?? empty)
                .Where(x => !inThisRun.Contains(x.Key))
                .OrderByDescending(x => x.Value)
                .Select(x => new OtherClientDays
                {
                    ClientId = x.Key == Guid.Empty ? null : x.Key,
                    WorkedDays = x.Value,
                    UnpaidDays = unpaid.GetValueOrDefault(x.Key),
                })
                .ToList();
            plans[employee.Id] = new CarrierPlan(
                decision, period, others, decision.Carries ? others.Sum(x => x.UnpaidDays) : 0);
        }
        return plans;
    }

    // The unique (employee, period) index rejecting a second carrier. Provider-neutral: SQL Server
    // says "duplicate key", MySQL "Duplicate entry", SQLite "UNIQUE constraint failed".
    private static bool IsUniqueViolation(Microsoft.EntityFrameworkCore.DbUpdateException ex)
    {
        var message = ex.GetBaseException().Message;
        return message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unique", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPayrollPeriod(DateRangePayload payload)
    {
        return string.Concat(
               payload.FromDate.ToString("MMM-dd-yyyy"), " ",
               payload.ToDate.ToString("MMM-dd-yyyy"),
               string.Empty);
    }
}
