
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
    // Per-client maximum monthly EE (employee) statutory deduction, aggregated across all
    // cutoffs in the month — absent key (or a value <= 0) means uncapped. See
    // StatutoryCapHelper.ApplyClientCap and Setup > Client > Settings > Statutory Capping.
    public Dictionary<ClientStatutoryCapKey, decimal> ClientStatutoryCaps { get; set; } = new();
    // Client.RetirementDaysPerYear keyed by Client.Id -- absent key or a null value means that
    // client gives no retirement benefit. See EmployeePayrollLineService.ComputeRetirementAccrual.
    public Dictionary<Guid, decimal?> ClientRetirementDaysPerYear { get; set; } = new();
    // Populated by PayrollService.LoadPostedPayrollAsync -- despite the name, this includes
    // still-unposted (draft) payrolls generated earlier this month, not just approved/Posted
    // ones. Kept that way deliberately so it stays scoped identically to SSSContribution/
    // PHICContribution/HDMFContribution below, which are also written at Generate time
    // regardless of posted status.
    public Dictionary<EmployeeKey, List<Payroll>> PostedPriorPayrolls { get; set; } = new();
    // Company > Cross-month statutory credit policy, so a prior payroll's statutory month can be
    // worked out the same way the current line's was (StatutoryMonthResolver.CreditDate).
    public CrossMonthStatutoryCreditPolicy StatutoryCreditPolicy { get; set; } = CrossMonthStatutoryCreditPolicy.CutoffStartMonth;
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
    //public Dictionary<EmployeeKey, List<WTaxContributionModel>> TaxContribution { get; set; } = new();
    //public List<ProratedAllowanceForSSS> ProratedAllowance { get; set; } = new();
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
    // The OT/ND pay method this day is priced under (Setup > Company Policy > OT/ND Calculation
    // Method). Stamped by the DTR day-type pay calculator pricing the day, so the day-type policies
    // (DayTypeHours) always branch the same way as the OT/ND buckets they pair with.
    public OtNdCalculationMethod OtNdCalculationMethod { get; set; } = OtNdCalculationMethodDefaults.Default;
    // True on an earnings-only run of an employee whose deductions another run carries
    // (DeductionCarrierMode). Per-payroll flat amounts such as COLA are paid once per cutoff, so the
    // carrier run pays them and this run does not.
    public bool IsEarningsOnly { get; set; }
}
public class DeductionPayloadContext : BasePayloadContext
{
    public EmployeeModelPayrollRun Employee { get; set; } = new();
    public CalculatorPayload Payload { get; set; } = new();
    public PayrollSummaryLine PayrollLine { get; set; } = new();
}
