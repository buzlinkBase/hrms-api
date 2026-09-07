namespace Hrms.Core.Services;

public class PayrollProcessorUtil
{
    public static decimal GetGross(PayrollSummaryLine payrollLine)
    {
        // GovernmentFundedLeavePay is deliberately excluded — it's a non-taxable government
        // benefit pass-through (see PayrollSummaryLine.GovernmentFundedLeavePay doc comment
        // and EmployeePayrollLineService.ApplyOneTimeLeavePayoutsToGross), added straight to
        // NetPay after deductions are computed off this Gross figure instead. Including it
        // here would wrongly subject it to SSS/PhilHealth/Pag-IBIG/WTax and double-count it
        // into NetPay.
        var gross = payrollLine.BasicPay
            + payrollLine.TimeHourPayResults.Sum(x => x.TotalExcludingBasic)
            + payrollLine.TotalAllIncome
            + payrollLine.CompanyFundedLeavePay
            ;
        return gross;
    }

    public static decimal GetNetPay(PayrollSummaryLine payrollLine, DeductionPipeData deductionPipeLine)
    {
        return payrollLine.GrossIncome - deductionPipeLine.RunningTotal;
    }
}
