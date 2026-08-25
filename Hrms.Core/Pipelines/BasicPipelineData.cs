

using Hrms.Domain.Entities;
using System.Collections.ObjectModel;
namespace Hrms.Core.Pipelines;

public interface IPipeData { }
public class LineCollection<T> : Collection<T>, IPipeData { }
public class BasicPipelineData : IPipeData
{
    public decimal Value { get; set; }
    public PayType PayType { get; set; }
}
public class AllowancePipeData : IPipeData
{
    public decimal RunningTotal { get; set; }
    public decimal Cola { get; set; }
    public List<ProratedAllowanceForSSS> ProratedAllowances { get; set; } = new();
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
    public List<DeductionInfo> ScheduledDeductions { get; set; } = new();
    public SSSInfo SSS { get; set; } = new();
    public PHICInfo PHIC { get; set; } = new();
    public HDMFInfo HDMF { get; set; } = new();
    public WTaxInfo TaxInfo { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
