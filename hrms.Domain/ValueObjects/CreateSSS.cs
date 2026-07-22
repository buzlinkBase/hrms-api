namespace Hrms.Domain.ValueObjects;

public class CreateSSS
{
    public DateOnly EffectiveDate { get; set; }
    public decimal RangeFrom { get; set; }
    public decimal RangeTo { get; set; }
    public decimal MSC { get; set; }
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal EC { get; set; }

}
public class UpdateSSS : CreateSSS
{
    public Guid Id { get; set; }

}
public class SSSModel : UpdateSSS
{
    public decimal TotalContibution { get; set; }
}

public class SSSContributionModel
{
    public Guid Id { get; set; }
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
