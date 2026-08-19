using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

public class PayrollProcessorService
{
    private readonly DailyRecordService _dtrServie;
    private readonly PayrollRangeContextComposerService _payloadComposer;
    private readonly PayrollService _payrollService;
    private readonly IMapper _mapper;

    public PayrollProcessorService(
        PayrollRangeContextComposerService payloadComposer,
        DailyRecordService dtrServie,
        PayrollService payrollService,
        IMapper mapper)
    {
        _dtrServie = dtrServie;
        _payloadComposer = payloadComposer;
        _payrollService = payrollService;
        _mapper = mapper;
    }

    public async Task<List<Payroll>> GenerateAsync(PayrollRunPayload payload, CancellationToken token)
    {
        var lines = await CalculateAsync(payload, token);
        var payrolls = _mapper.Map<List<Payroll>>(lines);
        await _payrollService.SavePayrollsAsync(payrolls, token);
        return payrolls;
    }

    public async Task<List<PayrollSummaryLine>> CalculateAsync(PayrollRunPayload payload,
        CancellationToken token)
    {
        var payrollLines = new List<PayrollSummaryLine>();

        var (dtrs, fromDate, toDate) = await _dtrServie.LoadForPayrollRunAsync(payload.BatchCodes, token);
        if (dtrs == null || !dtrs.Any()) return payrollLines;

        var dateRange = new DateRangePayload(fromDate, toDate);
        var period = BuildPayrollPeriod(dateRange);
        var batch = Guid.NewGuid();

        var employees = dtrs.Values
            .SelectMany(x => x.Select(x => x.Employee))
            .Distinct()
            .ToList();

        if (!employees.Any()) return payrollLines;

        var rangePayload = await _payloadComposer.ComposeAsync(dateRange, employees, token)
                          ?? throw new Exception("Unable to load range payload");

        foreach (var employee in employees)
        {
            if (employee == null) continue;
            var payrollLine = InitializePayrollLine(dateRange, employee, batch, period);
            if (dtrs.TryGetValue(new EmployeeKey(employee.Id), out var empDtr))
            {
                ComputeBasicSalary(dateRange, empDtr, employee, rangePayload, payrollLine);
            }
            ComputeAllowances(dateRange, rangePayload, employee, payrollLine);
            ComputeDeductions(rangePayload, employee, payrollLine);
            ApplySalaryAdjustments(rangePayload, employee, payrollLine);
            payrollLines.Add(payrollLine);
        }
        return payrollLines;
    }

    private static string BuildPayrollPeriod(DateRangePayload payload) => string.Concat(
             payload.FromDate.ToString("MMM-dd-YY"),
             payload.ToDate.ToString("MMM-dd-YY"),
             string.Empty);

    private static PayrollSummaryLine InitializePayrollLine(
        DateRangePayload payload,
        EmployeeModelPayrollRun employee,
        Guid batch,
        string period) =>
        new PayrollSummaryLine
        {
            PayrollPeriod = period,
            PayPeriodStart = payload.FromDate,
            PayPeriodEnd = payload.ToDate,
            EmployeeId = employee.Id,
            FullName = employee.FullName,
            BatchCode = batch,
            PayrollDate = payload.ToDate,
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
        var employeeBasicCalc = CalculateBasicRate(payload, dtrs, employee, rangePayload);
        payrollLine.BasicSalary = employeeBasicCalc.Sum(x => x.Basic);
        payrollLine.BasicSalaryItems = employeeBasicCalc;
        payrollLine.LateAmount = employeeBasicCalc.Sum(x => x.LateHourInfo.Amount);
        payrollLine.LateHours = employeeBasicCalc.Sum(x => x.LateHourInfo.Hour);
        payrollLine.OvertimeHour = employeeBasicCalc.Sum(x => x.OTHourInfo.Hour);
        payrollLine.OvertimePay = employeeBasicCalc.Sum(x => x.OTHourInfo.Amount);
        payrollLine.UnderTimeAmount = employeeBasicCalc.Sum(x => x.UTHourInfo.Amount);
        payrollLine.UnderTimeHours = employeeBasicCalc.Sum(x => x.UTHourInfo.Hour);
        payrollLine.NightDifferentialHour = employeeBasicCalc.Sum(x => x.NightDiffInfo.Hour);
        payrollLine.NightDifferentialPay = employeeBasicCalc.Sum(x => x.NightDiffInfo.Amount);
        payrollLine.Absences = employeeBasicCalc.Sum(x => x.AbsentInfo.Amount);
        payrollLine.AbsentCount = employeeBasicCalc.Sum(x => x.AbsentInfo.Count);

        // Aggregate per-type DTR hours directly from raw records
        payrollLine.RegularNetHours = dtrs.Sum(x => x.RegularNetHours);
        payrollLine.RegularOTHours = dtrs.Sum(x => x.RegularOTHours);
        payrollLine.RegularNDHours = dtrs.Sum(x => x.RegularNDHours);
        payrollLine.RegularNDOTHours = dtrs.Sum(x => x.RegularNDOTHours);

        payrollLine.RestDayHours = dtrs.Sum(x => x.RestDayHours);
        payrollLine.RestDayOTHours = dtrs.Sum(x => x.RestDayOTHours);
        payrollLine.RestDayNDHours = dtrs.Sum(x => x.RestDayNDHours);
        payrollLine.RestDayNDOTHours = dtrs.Sum(x => x.RestDayNDOTHours);

        payrollLine.LegalHolHours = dtrs.Sum(x => x.LegalHolHours);
        payrollLine.LegalHolOTHours = dtrs.Sum(x => x.LegalHolOTHours);
        payrollLine.LegalHolNightDiffHours = dtrs.Sum(x => x.LegalHolNightDiffHours);
        payrollLine.LegalHolNightDiffOTHours = dtrs.Sum(x => x.LegalHolNightDiffOTHours);

        payrollLine.SpecialHolHours = dtrs.Sum(x => x.SpecialHolHours);
        payrollLine.SpecialHolOTHours = dtrs.Sum(x => x.SpecialHolOTHours);
        payrollLine.SpecialHolNightDiffHours = dtrs.Sum(x => x.SpecialHolNightDiffHours);
        payrollLine.SpecialHolNightDiffOTHours = dtrs.Sum(x => x.SpecialHolNightDiffOTHours);

        payrollLine.RestLegalDayHours = dtrs.Sum(x => x.RestLegalDayHours);
        payrollLine.RestLegalDayOTHours = dtrs.Sum(x => x.RestLegalDayOTHours);
        payrollLine.RestLegalDayNDHours = dtrs.Sum(x => x.RestLegalDayNDHours);
        payrollLine.RestLegalDayNDOTHours = dtrs.Sum(x => x.RestLegalDayNDOTHours);

        payrollLine.RestSpecialDayHours = dtrs.Sum(x => x.RestSpecialDayHours);
        payrollLine.RestSpecialDayOTHours = dtrs.Sum(x => x.RestSpecialDayOTHours);
        payrollLine.RestSpecialDayNDHours = dtrs.Sum(x => x.RestSpecialDayNDHours);
        payrollLine.RestSpecialDayNDOTHours = dtrs.Sum(x => x.RestSpecialDayNDOTHours);
    }

    private List<BasicRateModel> CalculateBasicRate(
        DateRangePayload payload,
        List<DailyRecordRunModel> dtrs,
        EmployeeModelPayrollRun employee,
        CalculatorPayload calcPayload)
    {
        var processor = new BasicPayrollCalculator();
        var basicResultMoel = new List<BasicRateModel>();

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

            var result = processor.Calculate(context);
            if (result == null) continue;

            result.Date = date;
            basicResultMoel.Add(result);

        }
        return basicResultMoel;
    }

    private void ComputeAllowances(
        DateRangePayload payload,
        CalculatorPayload rangePayload,
        EmployeeModelPayrollRun employee,
        PayrollSummaryLine payrollLine)
    {
        var pp = new PayrollCalcPayload(payload.FromDate, payload.ToDate, null, null,null, null);
        var context = new PayrollContextBuilder().SetEmployee(employee).SetPayload(rangePayload).Build();
        var IncomeCalculator = new AllowancesCalculator();
        var IncomeCalcResult = IncomeCalculator.Calculate(context);
        payrollLine.Cola = IncomeCalcResult.Cola;
        payrollLine.TotalDeminimises = IncomeCalcResult.Deminimises.Sum(x => x.Amount);
        payrollLine.TotalOtherIncome = IncomeCalcResult.OtherIncome.Sum(x => x.Amount);
        payrollLine.TotalCommissions = IncomeCalcResult.Commissions.Sum(x => x.Amount);
        payrollLine.TotalBonuses = IncomeCalcResult.Bonuses.Sum(x => x.Amount);
        payrollLine.Reimbursement = IncomeCalcResult.Reimbursements.Sum(x => x.Amount);
        payrollLine.TotalRegularAllowances = IncomeCalcResult.RegularAllowances.Sum(x => x.Amount);
        payrollLine.OtherIncomeCollection = IncomeCalcResult.AllIncome;
        payrollLine.RegularAllowanceProrated = CaptureProratedAllowance(pp, payrollLine.TotalRegularAllowances);
        payrollLine.GrossIncome = PayrollProcessorUtil.GetGrossIncome(IncomeCalcResult, payrollLine.BasicSalaryItems);
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

        var deductionPipeLine = new DeductionCalculator().Calculate(deductionContext);
        payrollLine.DeductionCollection = deductionPipeLine.ScheduledDeductions;
        payrollLine.NetPay = PayrollProcessorUtil
            .GetNetPay(payrollLine, deductionPipeLine);

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
            switch (adj.AdjustmentType)
            {
                case SalaryAdjustmentType.Salary:
                    payrollLine.BasicSalary += adj.Amount;
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
        List<BasicRateModel> basics)
    {
        return basics.Sum(x => x.TimeBaseGross) + incomes.RunningTotal;
    }
    public static decimal GetNetPay(PayrollSummaryLine payrollLine, DeductionPipeData deductionPipeLine)
    {
        return payrollLine.GrossIncome - deductionPipeLine.RunningTotal;
    }
}
public record MonthYear(int Month, int Year);
