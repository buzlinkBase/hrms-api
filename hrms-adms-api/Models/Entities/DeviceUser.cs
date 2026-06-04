namespace Hrms.adms.Models.Entities;

public class DeviceUser : BaseEntity
{
    public string SN { get; set; } = string.Empty;
    public string UserPin { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Password { get; set; } = string.Empty;
    public string Card { get; set; } = string.Empty;
}
