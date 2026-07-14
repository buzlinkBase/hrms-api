namespace Hrms.adms.Models.DTO;

public class CreateBiometricDevice
{
    public string? DeviceName { get; set; } = string.Empty;
    public string SN { get; set; } = string.Empty;
    public string? Description { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? AreaId { get; set; }
    public string Status { get; set; } = "Active";
}
public class UpdateBiometricDevice : CreateBiometricDevice
{
    public Guid Id { get; set; }
}

public class BiometricDeviceModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string SN { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string OemVendor { get; set; } = string.Empty;
    //public string FwVersion { get; set; } = string.Empty;
    //public string PushVersion { get; set; } = string.Empty;
    //public string? RegDeviceType { get; set; } = string.Empty;
    //public int LanguageCode { get; set; } = 69;
    public Guid? BranchId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? OperationAreaId { get; set; }
    public string Status { get; set; } = "Active";
}