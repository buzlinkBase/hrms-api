namespace Hrms.adms.Models.Entities;

public class BiometricDevice : BaseEntity, IEntityTenant
{ 
    public string SN { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string OemVendor { get; set; } = string.Empty;
    public string FwVersion { get; set; } = string.Empty;
    public string PushVersion { get; set; } = string.Empty;
    public string? RegDeviceType { get; set; } = string.Empty;
    public int LanguageCode { get; set; } =  69;
    public Guid? BranchId { get; set; }  
    public Guid? ClientId { get; set; }  
    public Guid? DepartmentId { get; set; }
    public Guid? OperationAreaId  { get; set; }
    public string Status { get; set; } = "Active";
}
public class SystemCounters : BaseEntity
{
    public string SN { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public int MaxAttLogCount { get; set; }
    public int UserCount { get; set; }
    public int MaxUserCount { get; set; }
}
public class BiometricDetail : BaseEntity
{
    public string SN { get; set; } = string.Empty;
    public  BiometricType Type { get; set; }
    public bool Enabled { get; set; }
    public string Version { get; set; } = string.Empty;
    public int Count { get; set; }
    public int? MaxCount { get; set; } // Nullable for empty values like MaxPvCount
}

public class Biometrics : BaseEntity
{
    public string SN { get; set; } = string.Empty;
    public virtual BiometricDetail Fingerprint { get; set; } = new();
    public virtual BiometricDetail Face { get; set; } = new();
    public virtual BiometricDetail FingerVein { get; set; } = new();
    public virtual BiometricDetail PalmVein { get; set; } = new();
}
public class PhotosAndMedia : BaseEntity
{
    public string SN { get; set; } = string.Empty;
    public bool PhotoFunctionEnabled { get; set; }
    public bool UserPicUrlFunctionEnabled { get; set; }
    public int MaxUserPhotoCount { get; set; }
}
public class FeaturesAndProtocols : BaseEntity
{
    public string SN { get; set; } = string.Empty;
    public bool VisilightFun { get; set; }
    public bool? VisualIntercomFunOn { get; set; }
    public string? VideoTid { get; set; }
    public string? VideoProtocol { get; set; }
    public bool SubcontractingUpgradeFunOn { get; set; }
    public virtual QrCodeConfig QrCode { get; set; } = new();
    public virtual ThermalAndMaskConfig ThermalAndMask { get; set; } = new();
    public virtual MultiBioSupport ConfigSupport { get; set; } = new();
}
public class QrCodeConfig : BaseEntity
{
    public string SN { get; set; } = string.Empty;
    public bool? IsSupported { get; set; }
    public bool? Enabled { get; set; }
    public string? DecryptFunList { get; set; }
}
public class ThermalAndMaskConfig : BaseEntity
{
    public string SN { get; set; } = string.Empty;
    public bool? IrTempDetectionFunOn { get; set; }
    public bool? MaskDetectionFunOn { get; set; }
}
public class MultiBioSupport : BaseEntity
{
    public string SN { get; set; } = string.Empty;
    public virtual List<int> DataSupport { get; set; } = new();
    public virtual List<int> PhotoSupport { get; set; } = new();
}
