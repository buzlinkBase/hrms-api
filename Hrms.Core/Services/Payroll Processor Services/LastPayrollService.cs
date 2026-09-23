using DTR.Core;
using Hrms.Core.Policies.DeductionPolicies;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// Last Pay / Final Pay (DOLE Labor Advisory 06-20) for a separated employee: prorated 13th
// month pay (basic pay actually earned up to DateResigned / 12) + cash conversion of
// convertible leave credits + cash-out of the accrued RetirementFund.Balance (non-taxable,
// added to NetPay rather than Gross), minus outstanding loan balance (informational netting
// only — the loan ledger itself is untouched). Shares the SAME annual exemption ceiling as 13th month
// (see ThirteenthMonthCeilingCalculator), combined with Special Bonuses already paid that
// year. Deliberately does NOT compute final DTR-attendance wages — those still flow through
// the existing regular payroll run from whatever DTR batch covers the employee's last days
// worked, the same relationship 13th Month Pay already has to regular payroll. One-time-ever
// per employee (not annual), guarded via GetLastPayPaidEmployeeIdsAsync. Extracted from
// PayrollProcessorService, which is now a thin Facade delegating here.
public class LastPayrollService
{
    private readonly EmployeeService _employeeService;
    private readonly PayrollService _payrollService;
    private readonly LeaveLedgerService _leaveLedgerService;
    private readonly DeductionAplDtlService _deductionAplDtlService;
    private readonly GeneralSettingService _generalSettingService;
    private readonly TaxService _taxService;
    private readonly PayrollBatchService _payrollBatchService;
    private readonly PayrollReportService _payrollReportService;
    private readonly IDailyRateResolver _dailyRateResolver;
    private readonly IMapper _mapper;
    private readonly StatutoryContributionLedgerService _statutoryLedgerService;
    private readonly SalaryAdjustmentService _salaryAdjustmentService;
    private readonly IncomeAplDtlService _incomeAplDtlService;
    private readonly PayrollInputConsumptionService _consumptionService;
    private readonly DailyRecordService _dtrService;
    private readonly YearLockService _yearLockService;
    private readonly ApprovalEngineService _approvalEngine;

    public LastPayrollService(
        EmployeeService employeeService,
        PayrollService payrollService,
        LeaveLedgerService leaveLedgerService,
        DeductionAplDtlService deductionAplDtlService,
        GeneralSettingService generalSettingService,
        TaxService taxService,
        PayrollBatchService payrollBatchService,
        PayrollReportService payrollReportService,
        IDailyRateResolver dailyRateResolver,
        IMapper mapper,
        StatutoryContributionLedgerService statutoryLedgerService,
        SalaryAdjustmentService salaryAdjustmentService,
        IncomeAplDtlService incomeAplDtlService,
        PayrollInputConsumptionService consumptionService,
        DailyRecordService dtrService,
        YearLockService yearLockService,
        ApprovalEngineService approvalEngine)
    {
        _employeeService = employeeService;
        _payrollService = payrollService;
        _leaveLedgerService = leaveLedgerService;
        _deductionAplDtlService = deductionAplDtlService;
        _generalSettingService = generalSettingService;
        _taxService = taxService;
        _payrollBatchService = payrollBatchService;
        _payrollReportService = payrollReportService;
        _dailyRateResolver = dailyRateResolver;
        _mapper = mapper;
        _statutoryLedgerService = statutoryLedgerService;
        _salaryAdjustmentService = salaryAdjustmentService;
        _incomeAplDtlService = incomeAplDtlService;
        _consumptionService = consumptionService;
        _dtrService = dtrService;
        _yearLockService = yearLockService;
        _approvalEngine = approvalEngine;
    }

    // Safety check for the Last Pay review screen — flags employees who have posted
    // attendance/DTR days after their last regular payroll's period end, through their
    // separation date, that no regular run has ever paid out. Informational only (never
    // blocks generation): some separations legitimately have zero final attendance, e.g. an
    // employee already on leave through their last day. Does not touch the DTR pipeline or
    // compute any wages — just counts already-posted days in that gap.
    public async Task<List<LastPayAttendanceWarning>> GetAttendanceWarningsAsync(List<Guid> employeeIds, CancellationToken token)
    {
        var employees = await _employeeService.GetSeparatedEmployeesForLastPayAsync(employeeIds, token);
        var warnings = new List<LastPayAttendanceWarning>();

        foreach (var employee in employees)
        {
            if (employee.DateResigned == null) continue;
            var asOfDate = DateOnly.FromDateTime(employee.DateResigned.Value);
            var lastCutoffEnd = await _payrollService.GetLatestRegularPayPeriodEndAsync(employee.Id, token);
            var checkFrom = lastCutoffEnd ?? DateOnly.MinValue;
            if (checkFrom >= asOfDate) continue;

            var unpaidDayCount = await _dtrService.CountPostedDaysAsync(employee.Id, checkFrom, asOfDate, token);
            if (unpaidDayCount == 0) continue;

            warnings.Add(new LastPayAttendanceWarning
            {
                EmployeeId = employee.Id,
                FullName = $"{employee.FirstName} {employee.LastName}".Trim(),
                LastRegularPayPeriodEnd = lastCutoffEnd,
                SeparationDate = asOfDate,
                UnpaidAttendanceDayCount = unpaidDayCount,
            });
        }

        return warnings;
    }

    // Cash Bond status for the Last Pay review screen — informational only, same role as
    // GetAttendanceWarningsAsync below: HR sees each separated employee's cash bond collected-
    // to-date vs. target and decides the refund manually (bonds are often conditional on
    // clearance/no accountabilities, which this system has no way to verify). Never applied to
    // NetPay or the deduction ledger — mirrors the "informational netting only" boundary already
    // documented on the outstanding-loan line in GenerateAsync below. Uses the latest separation
    // date among the selected employees as the "as of" cutoff, same simplifying convention
    // LoadOutstandingLoansAsync uses.
    public async Task<List<CashBondReportModel>> GetCashBondStatusAsync(List<Guid> employeeIds, CancellationToken token)
    {
        var employees = await _employeeService.GetSeparatedEmployeesForLastPayAsync(employeeIds, token);
        if (employees.Count == 0) return [];

        var latestSeparationDate = employees.Max(x => DateOnly.FromDateTime(x.DateResigned!.Value));
        return await _payrollReportService.GetCashBondReportAsync(latestSeparationDate, token, employeeIds);
    }

    // Review-step data for the Last Pay generation screen — every SalaryAdjustment not yet
    // consumed by any payroll run (regular or a prior Last Pay) for these employees. HR
    // confirms which of these to fold in via LastPayRunPayload.SalaryAdjustmentIds; nothing
    // here is applied just by being returned.
    public Task<List<SalaryAdjustment>> GetAvailableSalaryAdjustmentsAsync(List<Guid> employeeIds, CancellationToken token) =>
        _salaryAdjustmentService.FindAvailableAsync(employeeIds, token);

    // Same idea for pending Other Income (allowance) schedule amounts.
    public Task<List<OtherIncomeSchedules>> GetAvailableOtherIncomeAsync(List<Guid> employeeIds, CancellationToken token) =>
        _incomeAplDtlService.FindAvailableAsync(employeeIds, token);

    public async Task<List<PayrollSummaryLine>> GenerateAsync(LastPayRunPayload payload, Guid generatedByEmployeeId, CancellationToken token)
    {
        if (payload.EmployeeIds is not { Count: > 0 })
        {
            throw new ValidationException("Select at least one separated employee to generate Last Pay for.");
        }

        var employees = await _employeeService.GetSeparatedEmployeesForLastPayAsync(payload.EmployeeIds, token);
        var employeeMap = employees.ToDictionary(x => x.Id);
        var missing = payload.EmployeeIds.Where(id => !employeeMap.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new ValidationException(
                "One or more selected employees are not eligible for Last Pay — they must be marked separated " +
                "(Terminated/Resigned/Retired/Deceased) with a Date Resigned on file.");
        }

        var alreadyPaid = await _payrollService.GetLastPayPaidEmployeeIdsAsync(token);
        var alreadyPaidSelected = payload.EmployeeIds.Where(alreadyPaid.Contains).ToList();
        if (alreadyPaidSelected.Count > 0)
        {
            throw new ValidationException(
                "Last Pay has already been generated for one or more selected employees. " +
                "Delete the existing run first if you need to regenerate it.");
        }

        var convertibleLeaveValue = await _leaveLedgerService.GetConvertibleLeaveValueAsync(payload.EmployeeIds, token);
        var retirementBalances = await _payrollService.GetRetirementBalancesAsync(payload.EmployeeIds, token);
        var outstandingLoans = await LoadOutstandingLoansAsync(payload.EmployeeIds, employees, token);

        // Only what HR explicitly confirmed in the review step gets applied — never an
        // implicit sweep of everything in range (see PayrollInputConsumptionService's doc
        // comment on why Last Pay's window is too wide for that). Re-checks
        // ConsumedByPayrollId == null (via FindByIdsAsync) so anything consumed elsewhere
        // between the review screen loading and this call is silently skipped.
        var confirmedSalaryAdjustments = payload.SalaryAdjustmentIds is { Count: > 0 }
            ? await _salaryAdjustmentService.FindByIdsAsync(payload.SalaryAdjustmentIds, token)
            : new List<SalaryAdjustment>();
        var salaryAdjustmentsByEmployee = confirmedSalaryAdjustments
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var confirmedOtherIncome = payload.OtherIncomeScheduleIds is { Count: > 0 }
            ? await _incomeAplDtlService.FindByIdsAsync(payload.OtherIncomeScheduleIds, token)
            : new List<OtherIncomeSchedules>();
        var otherIncomeByEmployee = confirmedOtherIncome
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var settings = await _generalSettingService.GetSettingsAsync(PayrollSettingsIdentity.IdentityType);
        var ceiling = settings.TryGetValue(PayrollSettingsIdentity.KeyThirteenthMonthExemptionCeiling, out var ceilingSetting)
                      && ceilingSetting.Value != null
            ? GeneralSettingsUtil.ParseDouble(ceilingSetting.Value, 90_000)
            : 90_000;

        var effectiveDate = payload.PayDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (await _yearLockService.IsYearLockedAsync(effectiveDate.Year, token))
        {
            throw new ValidationException(
                $"Payroll for {effectiveDate.Year} is locked — the Year-End Tax Adjustment has already been " +
                "posted for this year. Reopen the year first if changes are required.");
        }

        var taxTable = await _taxService.LoadForPayrollrunAsync(token);
        var batchId = Guid.CreateVersion7();

        // Employees separated on the same date share the same GetThirteenthMonthAsync
        // result set — cached per (year, asOfDate) so a batch of same-day separations
        // doesn't re-run the same query once per employee.
        var thirteenthMonthFiguresCache = new Dictionary<DateOnly, List<ThirteenthMonthModel>>();

        var lines = new List<PayrollSummaryLine>();
        foreach (var employeeId in payload.EmployeeIds)
        {
            var employee = employeeMap[employeeId];
            var asOfDate = DateOnly.FromDateTime(employee.DateResigned!.Value);
            var periodStart = new DateOnly(asOfDate.Year, 1, 1);

            if (!thirteenthMonthFiguresCache.TryGetValue(asOfDate, out var figures))
            {
                figures = await _payrollReportService.GetThirteenthMonthAsync(asOfDate.Year, token, asOfDate);
                thirteenthMonthFiguresCache[asOfDate] = figures;
            }
            // Selectable per the request — HR can exclude either lump-sum component from a
            // given run (e.g. it was already paid out separately). TotalSpecialBonusesForYear
            // still reflects reality regardless, since it's about ceiling room already
            // consumed elsewhere this year, not about what this run is choosing to pay.
            var proratedThirteenthMonth = payload.IncludeThirteenthMonth
                ? figures.FirstOrDefault(x => x.EmployeeId == employeeId)?.ThirteenthMonthPay ?? 0
                : 0;
            var totalSpecialBonusesForYear = figures.FirstOrDefault(x => x.EmployeeId == employeeId)?.TotalSpecialBonusesForYear ?? 0;

            convertibleLeaveValue.TryGetValue(employeeId, out var leaveValue);
            var dailyRate = _dailyRateResolver.Resolve(employee, asOfDate);
            var leaveConversion = payload.IncludeLeaveConversion ? leaveValue * dailyRate : 0;
            retirementBalances.TryGetValue(employeeId, out var retirementBalance);
            var retirementPayout = payload.IncludeRetirementPayout ? retirementBalance : 0;
            outstandingLoans.TryGetValue(employeeId, out var outstandingLoanBalance);

            var gross = proratedThirteenthMonth + leaveConversion;
            var remainingCeiling = ThirteenthMonthCeilingCalculator.ComputeRemainingThirteenthMonthCeiling((decimal)ceiling, totalSpecialBonusesForYear);
            var (nonTaxable, taxable) = ThirteenthMonthCeilingCalculator.ComputeThirteenthMonthTaxSplit(gross, remainingCeiling);

            var wtaxResult = new DeductionPipeData { RemainingGrossBalance = taxable };
            if (taxable > 0 && employee.TaxRate != null)
            {
                employee.PayrollFrequency = PayrollFrequency.MONTHLY;
                var wtaxContext = new DeductionPayloadContext
                {
                    Employee = employee,
                    Payload = new CalculatorPayload
                    {
                        FromDate = periodStart,
                        ToDate = asOfDate,
                        TaxTableModel = taxTable,
                        CompanyPolicy = new CompanyPolicyRule(),
                    },
                    PayrollLine = new PayrollSummaryLine { GrossIncome = taxable },
                };
                wtaxResult = WTaxCalculatorFactory.Create(wtaxContext).Calculate(wtaxContext, wtaxResult);
            }
            // Loan balance is netted straight into RunningTotal so GetNetPay below subtracts
            // it alongside WTax in one step — informational only, never written back to the
            // loan ledger (DeductionApplicationDetail is never touched by this method).
            wtaxResult.RunningTotal += outstandingLoanBalance;

            var line = new PayrollSummaryLine
            {
                PayrollPeriod = $"Last Pay {asOfDate:yyyy-MM-dd}",
                PayPeriodStart = periodStart,
                PayPeriodEnd = asOfDate,
                PayrollDate = effectiveDate,
                StatutoryCreditDate = effectiveDate,
                PostingPeriod = effectiveDate,
                PayDate = payload.PayDate,
                PayrollBatchId = batchId,
                PayrollType = PayrollType.LastPay,
                Remarks = payload.Remarks,
                EmployeeId = employeeId,
                FullName = $"{employee.FirstName} {employee.LastName}".Trim(),
                SalaryType = employee.SalaryType,
                BasicPay = 0, // never counted toward a future 13th month/Alphalist figure
                GrossIncome = gross,
                NonTaxableBenefits = nonTaxable,
                TaxableBenefits = taxable,
                WithholdingTax = wtaxResult.TaxInfo?.TaxDue ?? 0,
                TotalLoans = outstandingLoanBalance,
                TotalDeductions = wtaxResult.RunningTotal,
                PayrollGroupId = employee.PayrollGroupId,
                AreaId = employee.AreaId,
                ClientId = employee.ClientId,
                RetirementPayout = retirementPayout,
            };
            line.NetPay = PayrollProcessorUtil.GetNetPay(line, wtaxResult);
            // Non-taxable (PH retirement benefits are generally tax-exempt at separation) --
            // added straight to NetPay, never to GrossIncome/the WTax base above, same
            // treatment as an employer-advanced government leave payout in the regular flow.
            // Settled (RetirementFund.Balance debited, RetirementLedger entry written) only at
            // Post time -- see PayrollService.ProcessRetirementFundActivityAsync.
            line.NetPay += retirementPayout;

            // HR-confirmed Salary Adjustments — reuses EmployeePayrollLineService's exact
            // apply logic (same untaxed-addition behavior as the regular flow) via a small
            // ad-hoc payload carrying just this employee's confirmed rows, the same
            // one-off-payload style already used above for the WTax calculator call.
            if (salaryAdjustmentsByEmployee.TryGetValue(employeeId, out var employeeAdjustments) && employeeAdjustments.Count > 0)
            {
                var adjustmentsPayload = new CalculatorPayload();
                adjustmentsPayload.SalaryAdjustments[new EmployeeKey(employeeId)] = employeeAdjustments;
                EmployeePayrollLineService.ApplySalaryAdjustments(adjustmentsPayload, employee, line);
            }

            // HR-confirmed Other Income (allowance) schedule amounts — same untaxed,
            // direct-addition treatment, respecting each row's own IsTaxable flag for the
            // informational Taxable/NonTaxableBenefits split.
            if (otherIncomeByEmployee.TryGetValue(employeeId, out var employeeOtherIncome) && employeeOtherIncome.Count > 0)
            {
                ApplyConfirmedOtherIncome(employeeOtherIncome, line);
            }

            EmployeePayrollLineService.ApplyTaxableIncomeSplit(line);
            lines.Add(line);
        }

        await _payrollBatchService.AddAsync(new PayrollBatch
        {
            Id = batchId,
            PayPeriodStart = lines.Min(x => x.PayPeriodStart),
            PayPeriodEnd = lines.Max(x => x.PayPeriodEnd),
            PayDate = payload.PayDate,
            PayrollType = PayrollType.LastPay,
            Remarks = payload.Remarks,
            GeneratedByEmployeeId = generatedByEmployeeId,
        }, token, commit: false);
        await _approvalEngine.StartAsync(ApprovalApplicationType.PayrollPosting, batchId, generatedByEmployeeId, token);

        var payrolls = _mapper.Map<List<Payroll>>(lines);
        foreach (var payroll in payrolls)
        {
            payroll.BatchCode = batchId.ToString();
        }
        await _payrollService.SavePayrollsAsync(payrolls, token, commit: false);
        // Atomic commit point — see PayrollBatchService.AddAsync's doc comment.
        await _payrollService.CommitChangesAsync(token);
        // SSS/PHIC/HDMF are never set on these lines; only the WTaxContribution row (needed
        // for BIR remittance reporting) gets written — same as ThirteenthMonthPayrollService.
        await _statutoryLedgerService.SaveAsync(lines, token);

        // Stamp ConsumedByPayrollId on exactly the ids each employee's own confirmed
        // selection included — never a date-range sweep (see PayrollInputConsumptionService).
        foreach (var payroll in payrolls)
        {
            salaryAdjustmentsByEmployee.TryGetValue(payroll.EmployeeId, out var employeeAdjustments);
            otherIncomeByEmployee.TryGetValue(payroll.EmployeeId, out var employeeOtherIncome);
            if ((employeeAdjustments?.Count ?? 0) == 0 && (employeeOtherIncome?.Count ?? 0) == 0) continue;

            await _consumptionService.MarkConsumedByIdsAsync(
                payroll.Id,
                employeeAdjustments?.Select(x => x.Id).ToList(),
                employeeOtherIncome?.Select(x => x.Id).ToList(),
                token);
        }

        return lines;
    }

    // Direct addition to Gross/Net, matching EmployeePayrollLineService.ApplySalaryAdjustments'
    // Allowance-branch treatment (untaxed — applied after tax computation, not re-run through
    // WTax) — but keyed off each schedule row's own IsTaxable flag for the informational
    // Taxable/NonTaxableBenefits split, since Other Income rows carry that per-row rather than
    // per-adjustment-type. internal (not private) so hrms.test can exercise this directly
    // without a DB.
    internal static void ApplyConfirmedOtherIncome(List<OtherIncomeSchedules> schedules, PayrollSummaryLine line)
    {
        foreach (var schedule in schedules)
        {
            line.GrossIncome += schedule.Amount;
            line.NetPay += schedule.Amount;
            line.TotalOtherIncome += schedule.Amount;
            if (schedule.IsTaxable) line.TaxableBenefits += schedule.Amount;
            else line.NonTaxableBenefits += schedule.Amount;
        }
    }

    // Sums outstanding (Balance > 0) loan installments per employee up to their own
    // separation date, via the same DeductionAplDtlService.LoadAsync the regular payroll
    // deduction pipeline uses — informational only for Last Pay.
    private async Task<Dictionary<Guid, decimal>> LoadOutstandingLoansAsync(
        List<Guid> employeeIds, List<EmployeeModelPayrollRun> employees, CancellationToken token)
    {
        var latestSeparationDate = employees
            .Where(x => employeeIds.Contains(x.Id))
            .Max(x => DateOnly.FromDateTime(x.DateResigned!.Value));
        var deductionsByEmployee = await _deductionAplDtlService.LoadAsync(employeeIds, DateOnly.MinValue, latestSeparationDate, token);

        return deductionsByEmployee.ToDictionary(
            kvp => kvp.Key.employeeId,
            kvp => kvp.Value.Where(x => x.Type == DeductionInfoType.Loan).Sum(x => x.Amount));
    }
}

// See LastPayrollService.GetAttendanceWarningsAsync.
public class LastPayAttendanceWarning
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    // Null = this employee has never had a regular payroll row at all.
    public DateOnly? LastRegularPayPeriodEnd { get; set; }
    public DateOnly SeparationDate { get; set; }
    public int UnpaidAttendanceDayCount { get; set; }
}
