namespace Hrms.adms.Models.DTO;

public class ZkDeviceModel
{
    public BiometricDevice DeviceInfo { get; set; } = new();
    public SystemCounters SystemCounters { get; set; } = new();
    public Biometrics Biometrics { get; set; } = new();
    public PhotosAndMedia PhotosAndMedia { get; set; } = new();
    public FeaturesAndProtocols FeaturesAndProtocols { get; set; } = new();

    public ZkDeviceModel()
    {
        DeviceInfo = new BiometricDevice();
        SystemCounters = new SystemCounters();
        PhotosAndMedia = new PhotosAndMedia();
        Biometrics = new Biometrics()
        {
            Fingerprint = new BiometricDetail { },
            FingerVein = new BiometricDetail { },
            PalmVein = new BiometricDetail { },
            Face = new BiometricDetail { },
        };
        FeaturesAndProtocols = new FeaturesAndProtocols
        {
            QrCode = new QrCodeConfig { },
            ThermalAndMask = new ThermalAndMaskConfig { },
            ConfigSupport = new MultiBioSupport
            {
                DataSupport = new List<int>(),
                PhotoSupport = new List<int>(),
            }
        };
    }
}