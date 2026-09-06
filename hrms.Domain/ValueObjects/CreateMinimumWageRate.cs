namespace Hrms.Domain.ValueObjects;

public class CreateMinimumWageRate
{
    public string RegionCode { get; set; } = string.Empty;
    public string RegionName { get; set; } = string.Empty;
    public decimal DailyRate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? WageOrderNo { get; set; }
}

public class UpdateMinimumWageRate : CreateMinimumWageRate
{
    public Guid Id { get; set; }
}

public class MinimumWageRateModel : UpdateMinimumWageRate
{
}
