namespace Hrms.Domain.ValueObjects;

public class CreateClientBillingInfo
{
    public string Tin { get; set; } = string.Empty;
    public string BillingAddress { get; set; } = string.Empty;
    public string BillingContactName { get; set; } = string.Empty;
    public string BillingEmail { get; set; } = string.Empty;
    public string BillingPhone { get; set; } = string.Empty;
    public int PaymentTermsDays { get; set; } = 30;
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public string Currency { get; set; } = "PHP";
    public string? Notes { get; set; }
}

public class UpdateClientBillingInfo : CreateClientBillingInfo
{
}

public class ClientBillingInfoModel : UpdateClientBillingInfo
{
}
