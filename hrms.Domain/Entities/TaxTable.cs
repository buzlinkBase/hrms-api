using BuzlinkRepository;

namespace Hrms.Domain.Entities;

[DisableSoftDelete]
public class TaxTable : BaseEntity
{
    public DateOnly EffectiveDate { get; set; }
    //public string SalaryType { get; set; } = string.Empty;
    public decimal RangeFrom { get; set; }
    public decimal RangeTo { get; set; }
    public decimal PercentageInAmountOf { get; set; }
    public decimal BaseTaxDue { get; set; }
    public decimal AddOnPercentage { get; set; }

}
public class WTaxContribution : BaseEntity,IDateFilter
{
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollFrom { get; set; }
    public DateOnly PayrollTo { get; set; }
    public DateOnly PayrollDate { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
}