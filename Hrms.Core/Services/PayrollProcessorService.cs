namespace Hrms.Core.Services;

public class PayrollProcessorService
{
    private readonly DailyRecordService _dtrServie;
    private readonly PayrollRangeContextComposerService _payloadComposer;
    public PayrollProcessorService(PayrollRangeContextComposerService payloadComposer, DailyRecordService dtrServie)
    {
        _dtrServie = dtrServie;
        _payloadComposer = payloadComposer;
    }

    public async Task<List<PayrollSummaryLine>> CalculateAsync(PayrollCalcPayload payload,
        CancellationToken token)
    {
        var payrollLines = new List<PayrollSummaryLine>();

        var dtrs = await _dtrServie.LoadForPayrollAsync(payload, token);
        if (dtrs == null || !dtrs.Any()) return payrollLines;

        var period = BuildPayrollPeriod(payload);
        var batch = Guid.NewGuid();

        var employees = dtrs.Values
            .SelectMany(x => x.Select(x => x.Employee))
            .Distinct()
            .ToList();

        if (!employees.Any()) return payrollLines;

        var rangePayload = await _payloadComposer.ComposeAsync(payload, employees, token)
                          ?? throw new Exception("Unable to load range payload");

        foreach (var employee in employees)
        {
            if (employee == null) continue;
            var payrollLine = InitializePayrollLine(payload, employee, batch, period);
            //get dtr
            if (dtrs.TryGetValue(new EmployeeKey(employee.Id), out var empDtr))
            {
                ComputeBasicSalary(payload, empDtr, employee, rangePayload, payrollLine);
            }
            //continue even if no dtr
            ComputeAllowances(payload, rangePayload, payrollLine);
            ComputeDeductions(rangePayload, employee, payrollLine);
            payrollLines.Add(payrollLine);
        }
        return payrollLines;
    }

    private static string BuildPayrollPeriod(PayrollCalcPayload payload) => string.Concat(
             payload.FromDate.ToString("MMM-dd-YY"),
             payload.ToDate.ToString("MMM-dd-YY"),
             payload.PayrollGroupId);

    private static PayrollSummaryLine InitializePayrollLine(
        PayrollCalcPayload payload,
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
        PayrollCalcPayload payload,
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
    }

    private List<BasicRateModel> CalculateBasicRate(
        PayrollCalcPayload payload,
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
        PayrollCalcPayload payload,
        CalculatorPayload rangePayload,
        PayrollSummaryLine payrollLine)
    {

        var context = new PayrollContextBuilder().SetPayload(rangePayload).Build();
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
        payrollLine.RegularAllowanceProrated = CaptureProratedAllowance(payload, payrollLine.TotalRegularAllowances);
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

        //TODO add empoyee side
        payrollLine.EmployerSSSContribution = deductionPipeLine.SSS.TotalER;
        payrollLine.EmployerPhilHealthContribution = deductionPipeLine.PHIC.Total;
        payrollLine.EmployerPagIbigContribution = deductionPipeLine.HDMF.Total;
        payrollLine.EmployerECContribution = deductionPipeLine.SSS.EC;

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
