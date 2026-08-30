
using Hrms.Domain.Entities;

namespace Hrms.Core.Pipelines;

public interface IPayloadContext { }
public abstract class BasePayloadContext : IPayloadContext
{
    public PayrollrunLedger Ledger { get; } = new();
    public SpecEvaluationCache SharedSpecCache { get; } = new();

}
public class CalculatorPayload : BasePayloadContext
{
    public Guid Id { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public Dictionary<RateType, decimal> PremiumRates { get; set; } = new();
    public Dictionary<ClientRateKey, decimal> ClientPremiumRates { get; set; } = new();
    public Dictionary<Guid, bool> ClientTreatNdotAsNdOnlyOverrides { get; set; } = new();
    public Dictionary<EmployeeKey, List<Payroll>> PostedPriorPayrolls { get; set; } = new();
    public Dictionary<Leavekey, List<LeaveApplication>> Leaves { get; set; } = new();
    public Dictionary<EmployeeLeaveCreditsKey, decimal> LeaveCredits { get; set; } = new();
    public Dictionary<EmployeeKey, List<OtherIncomeInfo>> Incomes { get; set; } = new();
    public Dictionary<EmployeeKey, List<DeductionInfo>> Deductions { get; set; } = new();
    public Dictionary<EmployeeKey, List<SalaryAdjustment>> SalaryAdjustments { get; set; } = new();
    // Approved LeaveApplications with PayoutMode.OneTime whose ReleasePayrollDate falls
    // within this run's date range — see PayrollProcessorService.ApplyOneTimeLeavePayouts.
    public Dictionary<EmployeeKey, List<LeaveApplication>> OneTimeLeavePayouts { get; set; } = new();
    public Dictionary<EmployeeKey, List<SSSContributionModel>> SSSContribution { get; set; } = new();
    public Dictionary<EmployeeKey, List<PHICContributionModel>> PHICContribution { get; set; } = new();
    public Dictionary<EmployeeKey, List<HDMFContributionModel>> HDMFContribution { get; set; } = new();
    public Dictionary<EmployeeKey, List<WTaxContributionModel>> TaxContribution { get; set; } = new();
    public Dictionary<HolidayKey, List<HolidayInfo>> Holidays { get; set; } = new();
    public List<ProratedAllowanceForSSS> ProratedAllowance { get; set; } = new();
    public List<SSSModel> SSSTableModel { get; set; } = new();
    public List<PHICModel> PHICTableModel { get; set; } = new();
    public List<HDMFModel> HDMFTableModel { get; set; } = new();
    public List<WTaxModel> TaxTableModel { get; set; } = new();
    public CompanyPolicyRule CompanyPolicy { get; set; } = new();
    public int DaysInMonth => DateTime.DaysInMonth(FromDate.Year, FromDate.Month);
    public double DaysDiffPayrollPeriod(PayrollContext context) => (context.Payload.ToDate.ToDateTime(TimeOnly.MinValue) - context.Payload.FromDate.ToDateTime(TimeOnly.MinValue)).TotalDays;

}

public class PayrollContext : BasePayloadContext
{
    public DailyRecordRunModel DailyRecord { get; set; } = new();
    public EmployeeModelPayrollRun Employee { get; set; } = new();
    public DateOnly PayrollDate { get; set; }
    public WorkType WorkType { get; set; }
    public CalculatorPayload Payload { get; set; } = new();
}
public class DeductionPayloadContext : BasePayloadContext
{
    public EmployeeModelPayrollRun Employee { get; set; } = new();
    public CalculatorPayload Payload { get; set; } = new();
    public PayrollSummaryLine PayrollLine { get; set; } = new();
}
