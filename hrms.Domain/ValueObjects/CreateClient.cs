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
    public Guid? HeadEmployeeId { get; set; }
    public decimal? RetirementDaysPerYear { get; set; }
    public decimal? UniformAllowance { get; set; }
    public BenefitAccrualBasis UniformAllowanceBasis { get; set; } = BenefitAccrualBasis.TenureMonths;
    // Project Sites to tag to this client. Null = leave the client's areas as they are (so a
    // caller that doesn't know about areas can never wipe them); an empty list = untag all.
    public List<Guid>? AreaIds { get; set; }
    // Allow taking a site that is tagged to another client; without it that is rejected.
    public bool ReassignAreas { get; set; }
}

public class UpdateClient : CreateClient
{
    public Guid Id { get; set; }
}

public class ClientModel : UpdateClient
{
    // Display link resolved from Client.LogoKey. Response-only: it is not on UpdateClient, so a
    // PUT can never overwrite the stored logo.
    public string? LogoUrl { get; set; }
    // Names of AreaIds, in the same order, for the client list. Response-only.
    public List<string> AreaNames { get; set; } = new();
}