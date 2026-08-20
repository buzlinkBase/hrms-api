using BuzlinkRepository;

namespace Hrms.Domain.Entities;

[DisableSoftDelete]
public class AnnualTaxTable : BaseEntity
{
    public DateOnly EffectiveDate { get; set; }
    public decimal RangeFrom { get; set; }
    public decimal RangeTo { get; set; }
    public decimal BaseTaxDue { get; set; }
    public decimal AddOnPercentage { get; set; }
}
