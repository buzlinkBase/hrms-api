

using BuzlinkRepository;

namespace Hrms.Domain.Entities;

[DisableSoftDelete]
public class SSSTable : BaseEntity
{

    public DateOnly EffectiveDate { get; set; }
    public decimal RangeFrom { get; set; }
    public decimal RangeTo { get; set; }
    public decimal MSC { get; set; }
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal EC { get; set; }
    public decimal TotalContibution { get; set; }

}

public class SSSContribution : BaseEntity,IDateFilter
{
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollFrom { get; set; }
    public DateOnly PayrollTo { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal EC { get; set; }
    public decimal TotalContibution { get; set; }
    public string Remarks { get; set; } = string.Empty;
}