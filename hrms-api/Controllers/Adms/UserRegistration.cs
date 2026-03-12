namespace Hrms.Api.Controllers.Adms;

public class UserRegistration
{
    public string UserPin { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Password { get; set; } = string.Empty;
    public string Card { get; set; } = string.Empty;
}

public record BioPayload(string SN, string RawData);
