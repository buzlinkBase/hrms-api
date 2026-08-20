namespace Hrms.Domain.ValueObjects;

public class GeneralSettingModel
{
    public string? IdentityId { get; set; }
    public required string Key { get; set; }
    public string? Value { get; set; }
    public string? Metadata { get; set; }
}
