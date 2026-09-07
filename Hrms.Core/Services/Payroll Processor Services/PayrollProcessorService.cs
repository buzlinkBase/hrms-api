using DTR.Core;
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
    private readonly PayrollBatchLifecycleService _batchLifecycleService;
    private readonly StatutoryContributionLedgerService _statutoryLedgerService;
    private readonly PayrollInputConsumptionService _consumptionService;

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
        PayrollBatchLifecycleService batchLifecycleService,
        StatutoryContributionLedgerService statutoryLedgerService,
        PayrollInputConsumptionService consumptionService)
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
        _batchLifecycleService = batchLifecycleService;
        _statutoryLedgerService = statutoryLedgerService;
        _consumptionService = consumptionService;
    }

    public async Task<List<PayrollSummaryLine>> GenerateAsync(PayrollRunPayload payload, Guid batch, CancellationToken token)
    {
        var usedBatchCodes = await _payrollBatchService.GetUsedDtrBatchCodesAsync(token);
        var alreadyPosted = payload.BatchCodes.Where(usedBatchCodes.Contains).ToList();
        if (alreadyPosted.Count > 0)
        {
            throw new ValidationException(
                $"Payroll has already been generated for DTR batch(es): {string.Join(", ", alreadyPosted)}. " +
                "Delete the existing payroll run first if you need to regenerate it.");
        }

        var lines = await CalculateAsync(payload, batch,token);
        if (lines.Count() == 0) return lines;
        var savingBatch = batch.ToString();

        // CalculateAsync already stamped every line's PayrollBatchId with the id it wants to
        // travel with (see EmployeePayrollLineService.Calculate -> InitializePayrollLine) — the
        // PayrollBatch header row must be inserted under that same id, otherwise
        // Payroll.PayrollBatchId (what the frontend groups by and posts/deletes against) never
        // matches any real PayrollBatch.Id.
        await _payrollBatchService.AddAsync(new PayrollBatch
        {
            Id = lines.First().PayrollBatchId,
            PayPeriodStart = lines.MinBy(x => x.PayPeriodStart)!.PayPeriodStart,
            PayPeriodEnd = lines.MaxBy(x => x.PayPeriodEnd)!.PayPeriodEnd,
            PayDate = payload.PayDate,
            DtrBatchCodes = string.Join(",", payload.BatchCodes),
            Remarks = payload.Remarks,
        }, token);

        // Generating no longer marks payroll as posted — a run stays an editable/deletable
        // draft (PayrollBatch.IsPosted defaults to false) until explicitly posted via
        // PostBatchAsync. Saving is not posting.
        var payrolls = _mapper.Map<List<Payroll>>(lines);
        foreach (var payroll in payrolls)
        {
            payroll.BatchCode = savingBatch;
        }
        await _payrollService.SavePayrollsAsync(payrolls, token);
        await _statutoryLedgerService.SaveAsync(lines, token);
        // Stamps ConsumedByPayrollId on whatever SalaryAdjustment/OtherIncomeSchedules rows
        // this run's own EmployeePayrollLineService.Calculate already matched by date range
        // moments earlier — see PayrollInputConsumptionService.
        await _consumptionService.MarkConsumedByDateRangeAsync(payrolls, token);

        // Saving a draft locks in the DTR it was built from — posts every DTR batch this run
        // used (idempotent/no-op on an already-posted batch) so it can't be edited or reused
        // by another Generate run while this draft exists. See DeleteBatchAsync for the
        // inverse: deleting a draft unposts these same DTR batches again.
        foreach (var batchCode in payload.BatchCodes)
        {
            await _dtrServie.PostAsync(batchCode, token);
        }
        return lines;
    }

    public Task<List<PayrollSummaryLine>> GenerateThirteenthMonthAsync(ThirteenthMonthRunPayload payload, CancellationToken token) =>
        _thirteenthMonthPayrollService.GenerateAsync(payload, token);

    public Task<List<PayrollSummaryLine>> GenerateLastPayAsync(LastPayRunPayload payload, CancellationToken token) =>
        _lastPayrollService.GenerateAsync(payload, token);

    // Review-step data for the Last Pay generation screen — see LastPayrollService's own
    // doc comments on these two.
    public Task<List<SalaryAdjustment>> GetAvailableSalaryAdjustmentsAsync(List<Guid> employeeIds, CancellationToken token) =>
        _lastPayrollService.GetAvailableSalaryAdjustmentsAsync(employeeIds, token);

    public Task<List<OtherIncomeSchedules>> GetAvailableOtherIncomeAsync(List<Guid> employeeIds, CancellationToken token) =>
        _lastPayrollService.GetAvailableOtherIncomeAsync(employeeIds, token);

    public Task<List<LastPayAttendanceWarning>> GetLastPayAttendanceWarningsAsync(List<Guid> employeeIds, CancellationToken token) =>
        _lastPayrollService.GetAttendanceWarningsAsync(employeeIds, token);

    public Task PostBatchAsync(Guid payrollBatchId, CancellationToken token) =>
        _batchLifecycleService.PostBatchAsync(payrollBatchId, token);

    public Task DeleteBatchAsync(Guid payrollBatchId, CancellationToken token) =>
        _batchLifecycleService.DeleteBatchAsync(payrollBatchId, token);

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
