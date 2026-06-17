namespace Hrms.Domain.ValueObjects;

public class CreateWTax
{
    public DateOnly EffectiveDate { get; set; }
    public string PayrollType { get; set; } = PayrollFrequency.SEMI_MONTHLY.ToString();
    public decimal RangeFrom { get; set; }
    public decimal RangeTo { get; set; }
    public decimal BaseTaxDue { get; set; }
    public decimal AddOnPercentage { get; set; }
}

public class UpdateWax : CreateWTax
{
    public Guid Id { get; set; }
}
public class WTaxModel : UpdateWax;

public class WTaxContributionModel
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string PayrollType { get; set; } = "";
    public DateOnly PayrollFrom { get; set; }
    public DateOnly PayrollTo { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal TaxDue { get; set; }
}
