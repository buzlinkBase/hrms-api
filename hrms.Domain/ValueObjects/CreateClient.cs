namespace Hrms.Domain.ValueObjects;

public class CreateClient
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public decimal? RetirementDaysPerYear { get; set; }
    public decimal? UniformAllowance { get; set; }
    public BenefitAccrualBasis UniformAllowanceBasis { get; set; } = BenefitAccrualBasis.TenureMonths;
}

public class UpdateClient : CreateClient
{
    public Guid Id { get; set; }
}

public class ClientModel : UpdateClient
{
}