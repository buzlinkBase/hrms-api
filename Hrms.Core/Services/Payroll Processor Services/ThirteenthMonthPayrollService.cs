using DTR.Core;
using Hrms.Core.Policies.DeductionPolicies;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// 13th month pay (PD 851): total BasicPay earned in the calendar year / 12 — a lump-sum,
// non-attendance-based payout, so unlike regular payroll there's no DTR batch to build from.
// Not subject to SSS/PhilHealth/Pag-IBIG (never invokes those calculators); the portion over
// ThirteenthMonthExemptionCeiling is run through the same WTax table calculator regular pay
// uses (not full BIR annualization — see plan notes). Reuses PayrollSummaryLine -> Payroll,
// the same PayrollBatch/Post/Delete lifecycle as regular payroll (see
// PayrollBatchLifecycleService) so Post/Delete/payslip/report infrastructure works unchanged.
// Extracted from PayrollProcessorService, which is now a thin Facade delegating here.
public class ThirteenthMonthPayrollService
{
    private readonly PayrollService _payrollService;
    private readonly IMapper _mapper;
    private readonly PayrollBatchService _payrollBatchService;
    private readonly PayrollReportService _payrollReportService;
    private readonly GeneralSettingService _generalSettingService;
    private readonly EmployeeService _employeeService;
    private readonly TaxService _taxService;
    private readonly StatutoryContributionLedgerService _statutoryLedgerService;

    public ThirteenthMonthPayrollService(
        PayrollService payrollService,
        IMapper mapper,
        PayrollBatchService payrollBatchService,
        PayrollReportService payrollReportService,
        GeneralSettingService generalSettingService,
        EmployeeService employeeService,
        TaxService taxService,
        StatutoryContributionLedgerService statutoryLedgerService)
    {
        _payrollService = payrollService;
        _mapper = mapper;
        _payrollBatchService = payrollBatchService;
        _payrollReportService = payrollReportService;
        _generalSettingService = generalSettingService;
        _employeeService = employeeService;
        _taxService = taxService;
        _statutoryLedgerService = statutoryLedgerService;
    }

    public async Task<List<PayrollSummaryLine>> GenerateAsync(ThirteenthMonthRunPayload payload, CancellationToken token)
    {
        var figures = await _payrollReportService.GetThirteenthMonthAsync(payload.Year, token);
        if (payload.EmployeeIds is { Count: > 0 })
            figures = figures.Where(x => payload.EmployeeIds.Contains(x.EmployeeId)).ToList();

        var employees = await _employeeService.GetForThirteenthMonthRunAsync(payload.PayrollGroupIds, payload.EmployeeIds, token);
        var employeeMap = employees.ToDictionary(x => x.Id);
        // Only employees eligible for 13th month (per GetForThirteenthMonthRunAsync's filter)
        // AND scoped by PayrollGroupIds, if provided.
        figures = figures.Where(x => employeeMap.ContainsKey(x.EmployeeId)).ToList();
        if (figures.Count == 0) return new List<PayrollSummaryLine>();

        var alreadyPaid = await _payrollService.GetThirteenthMonthPaidEmployeeIdsAsync(payload.Year, token);
        var pending = figures.Where(x => !alreadyPaid.Contains(x.EmployeeId)).ToList();
        if (pending.Count == 0)
        {
            throw new ValidationException(
                $"13th Month Pay for {payload.Year} has already been generated for every eligible employee in scope. " +
                "Delete the existing run first if you need to regenerate it.");
        }

        var settings = await _generalSettingService.GetSettingsAsync(PayrollSettingsIdentity.IdentityType);
        var ceiling = settings.TryGetValue(PayrollSettingsIdentity.KeyThirteenthMonthExemptionCeiling, out var ceilingSetting)
                      && ceilingSetting.Value != null
            ? GeneralSettingsUtil.ParseDouble(ceilingSetting.Value, 90_000)
            : 90_000;

        var effectiveDate = payload.PayDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var taxTable = await _taxService.LoadForPayrollrunAsync(effectiveDate, token);
        var periodStart = new DateOnly(payload.Year, 1, 1);
        var periodEnd = new DateOnly(payload.Year, 12, 31);
        var batchId = Guid.CreateVersion7();

        var lines = new List<PayrollSummaryLine>();
        foreach (var figure in pending)
        {
            var employee = employeeMap[figure.EmployeeId];
            var gross = figure.ThirteenthMonthPay;
            // Per Payroll Settings' documented rule, the exemption ceiling covers 13th month
            // pay COMBINED with Special Bonuses already paid this year — whatever room those
            // bonuses already consumed isn't available to this payout. Does not retroactively
            // touch the WTax already withheld on those bonus payroll runs, only this payout's
            // own split.
            var remainingCeiling = ThirteenthMonthCeilingCalculator.ComputeRemainingThirteenthMonthCeiling((decimal)ceiling, figure.TotalSpecialBonusesForYear);
            var (nonTaxable, taxable) = ThirteenthMonthCeilingCalculator.ComputeThirteenthMonthTaxSplit(gross, remainingCeiling);

            var wtaxResult = new DeductionPipeData { RemainingGrossBalance = taxable };
            if (taxable > 0 && employee.TaxRate != null)
            {
                // A lump-sum annual payout doesn't fit any Daily/Weekly/Semi-Monthly cutoff
                // table — Monthly-scale thresholds are the closest fit among the existing
                // tables. Mutating PayrollFrequency here is safe: this employee object was
                // just loaded for this run only and isn't reused elsewhere.
                employee.PayrollFrequency = PayrollFrequency.MONTHLY;
                var wtaxContext = new DeductionPayloadContext
                {
                    Employee = employee,
                    Payload = new CalculatorPayload
                    {
                        FromDate = periodStart,
                        ToDate = periodEnd,
                        TaxTableModel = taxTable,
                        CompanyPolicy = new CompanyPolicyRule(),
                    },
                    PayrollLine = new PayrollSummaryLine { GrossIncome = taxable },
                };
                wtaxResult = WTaxCalculatorFactory.Create(wtaxContext).Calculate(wtaxContext, wtaxResult);
            }

            var line = new PayrollSummaryLine
            {
                PayrollPeriod = $"13th Month Pay {payload.Year}",
                PayPeriodStart = periodStart,
                PayPeriodEnd = periodEnd,
                PayrollDate = effectiveDate,
                StatutoryCreditDate = effectiveDate,
                PostingPeriod = effectiveDate,
                PayDate = payload.PayDate,
                PayrollBatchId = batchId,
                PayrollType = PayrollType.ThirteenthMonth,
                Remarks = payload.Remarks,
                EmployeeId = figure.EmployeeId,
                FullName = figure.FullName,
                SalaryType = employee.SalaryType,
                BasicPay = 0, // never counted toward a future year's 13th month/Alphalist figure
                GrossIncome = gross,
                NonTaxableBenefits = nonTaxable,
                TaxableBenefits = taxable,
                WithholdingTax = wtaxResult.TaxInfo?.TaxDue ?? 0,
                PayrollGroupId = employee.PayrollGroupId,
                AreaId = employee.AreaId,
                ClientId = employee.ClientId,
            };
            line.NetPay = PayrollProcessorUtil.GetNetPay(line, wtaxResult);
            lines.Add(line);
        }

        await _payrollBatchService.AddAsync(new PayrollBatch
        {
            Id = batchId,
            PayPeriodStart = periodStart,
            PayPeriodEnd = periodEnd,
            PayDate = payload.PayDate,
            PayrollType = PayrollType.ThirteenthMonth,
            Remarks = payload.Remarks,
        }, token);

        var payrolls = _mapper.Map<List<Payroll>>(lines);
        foreach (var payroll in payrolls)
        {
            payroll.BatchCode = batchId.ToString();
        }
        await _payrollService.SavePayrollsAsync(payrolls, token);
        // SSS/PHIC/HDMF are never set on these lines, so SaveAsync's > 0 filters naturally
        // produce nothing for those three; only the WTaxContribution row (needed for BIR
        // remittance reporting) gets written.
        await _statutoryLedgerService.SaveAsync(lines, token);

        return lines;
    }
}
