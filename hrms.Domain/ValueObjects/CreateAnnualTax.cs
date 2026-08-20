namespace Hrms.Domain.ValueObjects;

public class CreateAnnualTax
{
    public DateOnly EffectiveDate { get; set; }
    public decimal RangeFrom { get; set; }
    public decimal RangeTo { get; set; }
    public decimal BaseTaxDue { get; set; }
    public decimal AddOnPercentage { get; set; }
}

public class UpdateAnnualTax : CreateAnnualTax
{
    public Guid Id { get; set; }
}

public class AnnualTaxModel : UpdateAnnualTax;
