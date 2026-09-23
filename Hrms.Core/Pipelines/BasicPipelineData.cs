

using Hrms.Domain.Entities;
using System.Collections.ObjectModel;
namespace Hrms.Core.Pipelines;

public interface IPipeData { }
public class LineCollection<T> : Collection<T>, IPipeData { }
public class BasicPipelineData : IPipeData
{
    public decimal Value { get; set; }
    public PayType PayType { get; set; }
    public decimal Worked { get; set; }
    public decimal UnWork { get; set; }
    public decimal OTPremium   { get; set; }
    public decimal NDPremium   { get; set; }

    // Flat, uncompounded segregated-recording figures -- NOT the same as OTPremium/NDPremium
    // above, which are deltas within the compounded rate stack used for actual pay (Value).
    // FlatOvertimeBase = hours * rawOtRate (HOLIDAY_OT/OVERTIME alone, ignoring day-type
    // compounding and any client override) -- set by both SingleCategoryOTPolicy (plain OT) and
    // SingleCategoryNDOTPolicy (NDOT). FlatNightDiffPremium = hours * (nightDiffRate - 1) against
    // the plain base pay -- set by SingleCategoryNDPolicy and SingleCategoryNDOTPolicy. See
    // DTRPayModel's matching OTBasePay/NDOTBasePay/NDPremiumPay/NDOTPremiumPay fields.
    public decimal FlatOvertimeBase { get; set; }
    public decimal FlatNightDiffPremium { get; set; }
}
public class AllowancePipeData : IPipeData
{
    public decimal RunningTotal { get; set; }
    public decimal Cola { get; set; }
    //public List<ProratedAllowanceForSSS> ProratedAllowances { get; set; } = new();
    public List<OtherIncomeInfo> AllIncome { get; set; } = new();
    public List<OtherIncomeInfo> OtherIncome { get; set; } = new();
    public List<OtherIncomeInfo> Reimbursements { get; set; } = new();
    public List<OtherIncomeInfo> Commissions { get; set; } = new();
    public List<OtherIncomeInfo> XmasBonuses { get; set; } = new();
    public List<OtherIncomeInfo> Bonuses { get; set; } = new();
    public List<OtherIncomeInfo> Deminimises { get; set; } = new();
    public List<OtherIncomeInfo> RegularAllowances { get; set; } = new();
}
public class DeductionPipeData : IPipeData
{
    public bool IsLimit { get; set; }
    public decimal RunningTotal { get; set; }
    public decimal RemainingGrossBalance { get; set; }
    public decimal CashBond { get; set; }
    public List<DeductionInfo> ScheduledDeductions { get; set; } = new();
    public SSSInfo SSS { get; set; } = new();
    public PHICInfo PHIC { get; set; } = new();
    public HDMFInfo HDMF { get; set; } = new();
    public WTaxInfo TaxInfo { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
