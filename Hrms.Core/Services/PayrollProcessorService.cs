using Hrms.Core.Policies.DeductionPolicies;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

public class PayrollProcessorService
{
    private readonly DailyRecordService _dtrServie;
    private readonly PayrollRangeContextComposerService _payloadComposer;
    private readonly PayrollService _payrollService;
    private readonly IMapper _mapper;
    private readonly ICalculator<DTRPayModel, PayrollContext> _basicPayrollCalculator;
    private readonly ICalculator<AllowancePipeData, PayrollContext> _allowancesCalculator;
    private readonly ICalculator<DeductionPipeData, DeductionPayloadContext> _deductionCalculator;
    private readonly IDailyRateResolver _dailyRateResolver;
    private readonly EmployeePayrollInclusionResolver _inclusionResolver;
    private readonly SSSContributionService _sssContributionService;
    private readonly PHICContributionService _phicContributionService;
    private readonly HDMFContributionService _hdmfContributionService;
    private readonly TaxContributionService _taxContributionService;

    public PayrollProcessorService(
        PayrollRangeContextComposerService payloadComposer,
        DailyRecordService dtrServie,
        PayrollService payrollService,
        IMapper mapper,
        ICalculator<DTRPayModel, PayrollContext> basicPayrollCalculator,
        ICalculator<AllowancePipeData, PayrollContext> allowancesCalculator,
        ICalculator<DeductionPipeData, DeductionPayloadContext> deductionCalculator,
        IDailyRateResolver dailyRateResolver,
        EmployeePayrollInclusionResolver inclusionResolver,
        SSSContributionService sssContributionService,
        PHICContributionService phicContributionService,
        HDMFContributionService hdmfContributionService,
        TaxContributionService taxContributionService)
    {
        _dtrServie = dtrServie;
        _payloadComposer = payloadComposer;
        _payrollService = payrollService;
        _mapper = mapper;
        _basicPayrollCalculator = basicPayrollCalculator;
        _allowancesCalculator = allowancesCalculator;
        _deductionCalculator = deductionCalculator;
        _dailyRateResolver = dailyRateResolver;
        _inclusionResolver = inclusionResolver;
        _sssContributionService = sssContributionService;
        _phicContributionService = phicContributionService;
        _hdmfContributionService = hdmfContributionService;
        _taxContributionService = taxContributionService;
    }

    public async Task<List<PayrollSummaryLine>> GenerateAsync(PayrollRunPayload payload, CancellationToken token)
    {
        var lines = await CalculateAsync(payload, token);
        var payrolls = _mapper.Map<List<Payroll>>(lines);
        foreach (var payroll in payrolls)
        {
            payroll.IsPosted = true;
        }
        await _payrollService.SavePayrollsAsync(payrolls, token);
        await SaveStatutoryContributionsAsync(lines, token);
        return lines;
    }

    // Persists one SSS/PHIC/HDMF ledger row per employee for this cutoff, so the next
    // cutoff's balance-netting (SSSHelper/PHICHelper/HDMFHelper.GetBalance) can see what's
    // already been withheld this month instead of always treating the target as untouched.
    private async Task SaveStatutoryContributionsAsync(List<PayrollSummaryLine> lines, CancellationToken token)
    {
        var sssRows = lines
            .Where(l => l.SSSContribution > 0 || l.EmployerSSSContribution > 0)
            .Select(l => new SSSContribution
            {
                EmployeeId = l.EmployeeId,
                PayrollFrom = l.PayPeriodStart,
                PayrollTo = l.PayPeriodEnd,
                PayrollDate = l.StatutoryCreditDate,
                EE = l.SSSContribution,
                ER = l.EmployerSSSContribution - l.EmployerECContribution,
                EC = l.EmployerECContribution,
                TotalContibution = l.SSSContribution + l.EmployerSSSContribution,
            })
            .ToList();

        var phicRows = lines
            .Where(l => l.PhilHealthContribution > 0 || l.EmployerPhilHealthContribution > 0)
            .Select(l => new PHICContribution
            {
                EmployeeId = l.EmployeeId,
                PayrollFrom = l.PayPeriodStart,
                PayrollTo = l.PayPeriodEnd,
                PayrollDate = l.StatutoryCreditDate,
                EmployeeShare = l.PhilHealthContribution,
                EmployerShare = l.EmployerPhilHealthContribution,
                TotalContribution = l.PhilHealthContribution + l.EmployerPhilHealthContribution,
            })
            .ToList();

        var hdmfRows = lines
            .Where(l => l.PagIbigContribution > 0 || l.EmployerPagIbigContribution > 0)
            .Select(l => new HDMFContribution
            {
                EmployeeId = l.EmployeeId,
                PayrollFrom = l.PayPeriodStart,
                PayrollTo = l.PayPeriodEnd,
                PayrollDate = l.StatutoryCreditDate,
                EmployeeShare = l.PagIbigContribution,
                EmployerShare = l.EmployerPagIbigContribution,
                TotalContribution = l.PagIbigContribution + l.EmployerPagIbigContribution,
            })
            .ToList();

        var taxRows = lines
            .Where(l => l.WithholdingTax > 0)
            .Select(l => new WTaxContribution
            {
                EmployeeId = l.EmployeeId,
                PayrollFrom = l.PayPeriodStart,
                PayrollTo = l.PayPeriodEnd,
                PayrollDate = l.PostingPeriod,
                Date = l.PayPeriodEnd,
                Amount = l.WithholdingTax,
            })
            .ToList();

        if (sssRows.Count > 0) await _sssContributionService.AddRangeAsync(sssRows, token);
        if (phicRows.Count > 0) await _phicContributionService.AddRangeAsync(phicRows, token);
        if (hdmfRows.Count > 0) await _hdmfContributionService.AddRangeAsync(hdmfRows, token);
        if (taxRows.Count > 0) await _taxContributionService.AddRangeAsync(taxRows, token);
    }

    public async Task<List<PayrollSummaryLine>> CalculateAsync(PayrollRunPayload payload, CancellationToken token)
    {
        var payrollLines = new List<PayrollSummaryLine>();
        var dtrRecords = await _dtrServie.LoadForPayrollRunAsync(payload.BatchCodes, token);
        if (dtrRecords.Records == null || !dtrRecords.Records.Any()) return payrollLines;
        var dateRange = new DateRangePayload(dtrRecords.FromDate, dtrRecords.ToDate);
        var period = BuildPayrollPeriod(dateRange);
        var batch = Guid.CreateVersion7();

        var employees = dtrRecords.Records.Values
            .SelectMany(x => x.Select(y => y.Employee))
            .DistinctBy(x => x.Id)
            .ToList();

        if (employees == null || !employees.Any())
        {
            return payrollLines;
        }

        // Resolve tenant-vs-employee Fixed-salary inclusion settings once, upstream —
        // every downstream DTR pay policy keeps reading employee.IsXxxIncluded unchanged.
        await _inclusionResolver.ApplyAsync(employees!, token);
        var rangePayload = await _payloadComposer.ComposePayload(dateRange, employees, token, payload.PayDate)
                          ?? throw new Exception("Unable to load range payload");

        // PayDate is user-supplied input, not a config fallback — silently defaulting to
        // ToDate here would quietly violate the company's chosen posting policy, so this
        // fails loudly instead (StatutoryCreditDateResolver's own fallback is defense in
        // depth only).
        var needsPayDate = rangePayload.CompanyPolicy.CrossMonthStatutoryCreditPolicy == CrossMonthStatutoryCreditPolicy.PayDate
            || rangePayload.CompanyPolicy.WTaxCrossMonthCreditPolicy == CrossMonthStatutoryCreditPolicy.PayDate;
        if (needsPayDate && payload.PayDate == null)
            throw new ValidationException("A Pay/Release Date is required to generate this payroll under the configured statutory posting policy.");

        foreach (var employee in employees)
        {
            if (employee == null) continue;
            if (!dtrRecords.Records.TryGetValue(new EmployeeKey(employee.Id), out var empDtr)) continue;
            employee.DailyRate = _dailyRateResolver.Resolve(employee, dateRange.FromDate);
            var payrollLine = InitializePayrollLine(
                dateRange, employee, batch, period,
                rangePayload.CompanyPolicy.CrossMonthStatutoryCreditPolicy,
                rangePayload.CompanyPolicy.WTaxCrossMonthCreditPolicy,
                payload.PayDate);
            ComputeBasicSalary(dateRange, empDtr, employee, rangePayload, payrollLine);
            ComputeAllowances(dateRange, rangePayload, employee, payrollLine);
            ComputeDeductions(rangePayload, employee, payrollLine);
            ApplySalaryAdjustments(rangePayload, employee, payrollLine);
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

    private static PayrollSummaryLine InitializePayrollLine(
        DateRangePayload payload,
        EmployeeModelPayrollRun employee,
        Guid batch,
        string period,
        CrossMonthStatutoryCreditPolicy creditPolicy,
        CrossMonthStatutoryCreditPolicy wtaxCreditPolicy,
        DateOnly? payDate) =>
        new PayrollSummaryLine
        {
            PayrollPeriod = period,
            PayPeriodStart = payload.FromDate,
            PayPeriodEnd = payload.ToDate,
            EmployeeId = employee.Id,
            FullName = employee.FullName,
            BatchCode = batch,
            PayrollDate = payload.ToDate,
            StatutoryCreditDate = StatutoryCreditDateResolver.Resolve(payload.FromDate, payload.ToDate, creditPolicy, payDate),
            PostingPeriod = StatutoryCreditDateResolver.Resolve(payload.FromDate, payload.ToDate, wtaxCreditPolicy, payDate),
            PayDate = payDate,
            PayrollGroupId = employee.PayrollGroupId,
            AreaId = employee.AreaId,
            ClientId = employee.ClientId
        };

    private void ComputeBasicSalary(
        DateRangePayload payload,
        List<DailyRecordRunModel> dtrs,
        EmployeeModelPayrollRun employee,
        CalculatorPayload rangePayload,
        PayrollSummaryLine payrollLine)
    {
        var employeeBasicCalc = CalculateDTRTimePay(payload, dtrs, employee, rangePayload);
        payrollLine.TimeHourPayResults = employeeBasicCalc;
        payrollLine.SalaryType = employee.SalaryType;
        payrollLine.DailyRate = employee.DailyRate;
        CalcBasicRate(payrollLine, employeeBasicCalc, employee);
        payrollLine.LateAmount = employeeBasicCalc.Sum(x => x.LateAmount);
        payrollLine.OvertimePay = employeeBasicCalc.Sum(x => x.TotalOT);
        payrollLine.UnderTimeAmount = employeeBasicCalc.Sum(x => x.UTAmount);
        payrollLine.NightDifferentialPay = employeeBasicCalc.Sum(x => x.TotalND);
        payrollLine.NightDifferentialOTPay = employeeBasicCalc.Sum(x => x.TotalNDOT);
        payrollLine.OTPremiumPay = employeeBasicCalc.Sum(x => x.OTPremiumPay);
        payrollLine.NDPremiumPay = employeeBasicCalc.Sum(x => x.NDPremiumPay);
        payrollLine.AbsencesAmount = employeeBasicCalc.Sum(x => x.AbsentAmount);

        payrollLine.RegularDayPay = employeeBasicCalc.Sum(x => x.RegularDayPay);
        payrollLine.RegularOTPay = employeeBasicCalc.Sum(x => x.RegularOTPay);
        payrollLine.RegularNDPay = employeeBasicCalc.Sum(x => x.RegularNDPay);
        payrollLine.RegularNDOTPay = employeeBasicCalc.Sum(x => x.RegularNDOTPay);

        payrollLine.RestDayPay = employeeBasicCalc.Sum(x => x.RestDayPay);
        payrollLine.RestDayOTPay = employeeBasicCalc.Sum(x => x.RestDayOTPay);
        payrollLine.RestDayNDPay = employeeBasicCalc.Sum(x => x.RestDayNDPay);
        payrollLine.RestDayNDOTPay = employeeBasicCalc.Sum(x => x.RestDayNDOTPay);

        payrollLine.LegalPay = employeeBasicCalc.Sum(x => x.LegalPay);
        payrollLine.LegalOTPay = employeeBasicCalc.Sum(x => x.LegalOTPay);
        payrollLine.LegalNDPay = employeeBasicCalc.Sum(x => x.LegalNDPay);
        payrollLine.LegalNDOTPay = employeeBasicCalc.Sum(x => x.LegalNDOTPay);

        payrollLine.SpecialPay = employeeBasicCalc.Sum(x => x.SpecialPay);
        payrollLine.SpecialOTPay = employeeBasicCalc.Sum(x => x.SpecialOTPay);
        payrollLine.SpecialNDPay = employeeBasicCalc.Sum(x => x.SpecialNDPay);
        payrollLine.SpecialNDOTPay = employeeBasicCalc.Sum(x => x.SpecialNDOTPay);

        payrollLine.RestLegalPay = employeeBasicCalc.Sum(x => x.RestLegalPay);
        payrollLine.RestLegalOTPay = employeeBasicCalc.Sum(x => x.RestLegalOTPay);
        payrollLine.RestLegalNDPay = employeeBasicCalc.Sum(x => x.RestLegalNDPay);
        payrollLine.RestLegalNDOTPay = employeeBasicCalc.Sum(x => x.RestLegalNDOTPay);

        payrollLine.RestSpecialPay = employeeBasicCalc.Sum(x => x.RestSpecialPay);
        payrollLine.RestSpecialOTPay = employeeBasicCalc.Sum(x => x.RestSpecialOTPay);
        payrollLine.RestSpecialNDPay = employeeBasicCalc.Sum(x => x.RestSpecialNDPay);
        payrollLine.RestSpecialNDOTPay = employeeBasicCalc.Sum(x => x.RestSpecialNDOTPay);

        payrollLine.DoubleLegalPay = employeeBasicCalc.Sum(x => x.DoubleLegalPay);
        payrollLine.DoubleLegalOTPay = employeeBasicCalc.Sum(x => x.DoubleLegalOTPay);
        payrollLine.DoubleLegalNDPay = employeeBasicCalc.Sum(x => x.DoubleLegalNDPay);
        payrollLine.DoubleLegalNDOTPay = employeeBasicCalc.Sum(x => x.DoubleLegalNDOTPay);

        payrollLine.RestDoubleLegalPay = employeeBasicCalc.Sum(x => x.RestDoubleLegalPay);
        payrollLine.RestDoubleLegalOTPay = employeeBasicCalc.Sum(x => x.RestDoubleLegalOTPay);
        payrollLine.RestDoubleLegalNDPay = employeeBasicCalc.Sum(x => x.RestDoubleLegalNDPay);
        payrollLine.RestDayNDOTPay = employeeBasicCalc.Sum(x => x.RestDayNDOTPay);
        payrollLine.UnpaidLeaves = employeeBasicCalc.Sum(x => x.UnpaidLeave);
        payrollLine.PaidLeaves = employeeBasicCalc.Sum(x => x.PaidLeave);
        payrollLine.HolidayPay = employeeBasicCalc.Sum(x => x.Holiday);

    }

    private List<DTRPayModel> CalculateDTRTimePay(
        DateRangePayload payload,
        List<DailyRecordRunModel> dtrs,
        EmployeeModelPayrollRun employee,
        CalculatorPayload calcPayload)
    {

        var basicResultMoel = new List<DTRPayModel>();
        for (var date = payload.FromDate; date <= payload.ToDate; date = date.AddDays(1))
        {
            var record = dtrs.FirstOrDefault(x => x.WorkDate == date && x.EmployeeId == employee.Id);
            if (record == null) continue;
            var context = new PayrollContextBuilder()
                .SetEmployee(employee)
                .SetDailyRecord(record)
                .SetWorkType(record)
                .SetPayrollDate(date)
                .SetPayload(calcPayload)
                .Build();

            var result = _basicPayrollCalculator.Calculate(context);
            if (result == null) continue;
            result.Date = date;
            result.DTRRef = record.BatchCode;
            result.DtrId = record.Id;
            result.SalaryType = employee.SalaryType;
            basicResultMoel.Add(result);
        }
        return basicResultMoel;
    }

    private void CalcBasicRate(PayrollSummaryLine payrollLine,
        List<DTRPayModel> TimeCalcResult,
        EmployeeModelPayrollRun employee)
    {
        if (employee.SalaryType != SalaryType.FIXED)
        {
            payrollLine.BasicPay = TimeCalcResult.Sum(x => x.RegularDayPay);
            return;
        }
        var divisor = GetDivisor(payrollLine.PayPeriodStart, employee);
        var basicTotal = employee.MonthlyRate / divisor;
        var deductions = Math.Max(0, TimeCalcResult.Sum(x => x.LateAmount + x.UTAmount + x.UnpaidLeave + x.AbsentAmount));
        basicTotal = basicTotal - deductions;
        payrollLine.BasicPay = basicTotal;

    }


    private int GetDivisor(DateOnly fromDate, EmployeeModelPayrollRun employee)
    {
        if (employee.PayrollGroup == null) return 2;
        switch (employee.PayrollGroup.PayrollFrequency)
        {
            case PayrollFrequency.DAILY:
                var days = DateTime.DaysInMonth(fromDate.Year, fromDate.Month);
                return days;
            case PayrollFrequency.WEEKLY:
                return 4;
            case PayrollFrequency.SEMI_MONTHLY:
                return 2;
            case PayrollFrequency.MONTHLY:
                return 1;
            default:
                return 2;
        }
    }

    private void ComputeAllowances(
        DateRangePayload payload,
        CalculatorPayload rangePayload,
        EmployeeModelPayrollRun employee,
        PayrollSummaryLine payrollLine)
    {
        var pp = new PayrollCalcPayload(payload.FromDate, payload.ToDate, null, null, null, null);
        var context = new PayrollContextBuilder()
            .SetEmployee(employee)
            .SetPayload(rangePayload)
            .Build();

        var IncomeCalcResult = _allowancesCalculator.Calculate(context);
        payrollLine.Cola = IncomeCalcResult.Cola;
        payrollLine.TotalDeminimises = IncomeCalcResult.Deminimises.Sum(x => x.Amount);
        payrollLine.TotalOtherIncome = IncomeCalcResult.OtherIncome.Sum(x => x.Amount);
        payrollLine.TotalCommissions = IncomeCalcResult.Commissions.Sum(x => x.Amount);
        payrollLine.TotalBonuses = IncomeCalcResult.Bonuses.Sum(x => x.Amount);
        payrollLine.Reimbursement = IncomeCalcResult.Reimbursements.Sum(x => x.Amount);
        payrollLine.TotalRegularAllowances = IncomeCalcResult.RegularAllowances.Sum(x => x.Amount);
        payrollLine.OtherIncomeCollection = IncomeCalcResult.AllIncome;
        payrollLine.RegularAllowanceProrated = CaptureProratedAllowance(pp, payrollLine.TotalRegularAllowances);
        payrollLine.GrossIncome = payrollLine.BasicPay +  PayrollProcessorUtil.GetGrossIncome(IncomeCalcResult, payrollLine.TimeHourPayResults);
        IdentifyTaxableIncome(payrollLine, IncomeCalcResult);

    }

    private void ComputeDeductions(
        CalculatorPayload rangePayload,
        EmployeeModelPayrollRun employee,
        PayrollSummaryLine payrollLine)
    {
        var deductionContext = new DeductionPayloadContextBuilder()
            .SetPayload(rangePayload)
            .SetEmployee(employee)
            .SetPayrollLine(payrollLine)
            .Build();

        var deductionPipeLine = _deductionCalculator.Calculate(deductionContext);
        payrollLine.DeductionCollection = deductionPipeLine.ScheduledDeductions;
        payrollLine.NetPay = PayrollProcessorUtil.GetNetPay(payrollLine, deductionPipeLine);
        payrollLine.SSSContribution = deductionPipeLine.SSS.EE;
        payrollLine.PhilHealthContribution = deductionPipeLine.PHIC.EE;
        payrollLine.PagIbigContribution = deductionPipeLine.HDMF.EE;
        payrollLine.WithholdingTax = deductionPipeLine.TaxInfo.TaxDue;
        payrollLine.OtherDeductions = deductionPipeLine.ScheduledDeductions.Sum(x => x.Amount);
        payrollLine.TotalDeductions = deductionPipeLine.RunningTotal;
        payrollLine.EmployerSSSContribution = deductionPipeLine.SSS.TotalER;
        payrollLine.EmployerPhilHealthContribution = deductionPipeLine.PHIC.Total;
        payrollLine.EmployerPagIbigContribution = deductionPipeLine.HDMF.Total;
        payrollLine.EmployerECContribution = deductionPipeLine.SSS.EC;
    }

    private static void ApplySalaryAdjustments(
        CalculatorPayload rangePayload,
        EmployeeModelPayrollRun employee,
        PayrollSummaryLine payrollLine)
    {
        if (!rangePayload.SalaryAdjustments.TryGetValue(new EmployeeKey(employee.Id), out var adjustments))
            return;

        foreach (var adj in adjustments)
        {
            //TODO add other salary type adjustment here
            switch (adj.AdjustmentType)
            {
                case SalaryAdjustmentType.Salary:
                    payrollLine.BasicPay += adj.Amount;
                    payrollLine.GrossIncome += adj.Amount;
                    payrollLine.NetPay += adj.Amount;
                    break;
                case SalaryAdjustmentType.Allowance:
                    payrollLine.TotalOtherIncome += adj.Amount;
                    payrollLine.GrossIncome += adj.Amount;
                    payrollLine.NetPay += adj.Amount;
                    break;
                case SalaryAdjustmentType.Deduction:
                    payrollLine.OtherDeductions += adj.Amount;
                    payrollLine.TotalDeductions += adj.Amount;
                    payrollLine.NetPay -= adj.Amount;
                    break;
            }
        }
    }

    private List<ProratedAllowanceModel> CaptureProratedAllowance(PayrollCalcPayload payload, decimal regularAllowance)
    {

        var result = new List<ProratedAllowanceModel>();
        var allDates = new List<DateOnly>();
        // Collect all dates in the range
        for (var date = payload.FromDate; date <= payload.ToDate; date = date.AddDays(1))
        {
            allDates.Add(date);
        }

        // Total days covered in the entire range
        int totalDays = allDates.Count;

        // Daily rate based on total allowance
        decimal dailyRate = regularAllowance / totalDays;

        // Group by month/year and compute prorated allowance
        var monthDays = allDates
            .GroupBy(x => new MonthYear(x.Month, x.Year))
            .Select(g => new { Key = g.Key, DayCount = g.Count() })
            .ToList();

        foreach (var item in monthDays)
        {
            result.Add(new ProratedAllowanceModel
            {
                Month = item.Key.Month,
                Year = item.Key.Year,
                Amount = dailyRate * item.DayCount // prorated value for that month
            });
        }
        return result;
    }
    private void IdentifyTaxableIncome(PayrollSummaryLine payrollLine, AllowancePipeData incomeInfo)
    {
        //var allincome = incomeInfo.OtherIncome
        //      .Union(incomeInfo.RegularAllowances)
        //      .Union(incomeInfo.Deminimises)
        //      .Union(incomeInfo.Commissions)
        //      .Union(incomeInfo.Bonuses)
        ;

        var allincome = incomeInfo.AllIncome;

        payrollLine.NonTaxableBenefits = allincome
            .Where(x => !x.Taxable)
            .Sum(x => x.Amount);

        payrollLine.TaxableBenefits = allincome
            .Where(x => x.Taxable)
            .Sum(x => x.Amount);
    }
}
public class ProratedAllowanceModel
{
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal Amount { get; set; }
}
public class PayrollProcessorUtil
{
    public static decimal GetGrossIncome(
        AllowancePipeData incomes,
        List<DTRPayModel> basics)
    {
        return basics.Sum(x => x.Gross) + incomes.RunningTotal;
    }
    public static decimal GetNetPay(PayrollSummaryLine payrollLine, DeductionPipeData deductionPipeLine)
    {
        return payrollLine.GrossIncome - deductionPipeLine.RunningTotal;
    }
}
public record MonthYear(int Month, int Year);
