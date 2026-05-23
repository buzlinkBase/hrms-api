using Hrms.adms.Models.DTO;
namespace Hrms.adms.Services.Processors;
public class OptionsProcessor : ICDataProcessor
{
    private readonly ILogger<OptionsProcessor> _logger;
    private readonly DeviceService _deviceService;

    public OptionsProcessor(ILogger<OptionsProcessor> logger,
        DeviceService deviceService)
    {
        _logger = logger;
        _deviceService = deviceService;
    }
    public async Task ProcessAsync(BioPayload payload, CancellationToken token = default)
    {
        await ParseAndStoreAsync(payload, token);
        await Task.CompletedTask;
    }

    private async Task ParseAndStoreAsync(BioPayload payload, CancellationToken token)
    {
        var rawData = payload.RawData;
        if (string.IsNullOrWhiteSpace(rawData))
        {
            return;
        }

        var deviceDataMap = rawData.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(kvp => kvp.Length == 2)
            .ToDictionary(
                kvp => kvp[0].TrimStart('~').Trim(), // Remove '~' and trim spaces from key
                kvp => kvp[1].Trim(),               // Trim spaces from value
                StringComparer.OrdinalIgnoreCase     // Allow case-insensitive lookups
            );

        // 2. Helper lambdas to map data types safely
        string? GetStr(string key) => deviceDataMap.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;
        int GetInt(string key) => deviceDataMap.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : 0;
        int? GetNullableInt(string key) => deviceDataMap.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : null;
        bool GetBool(string key) => deviceDataMap.TryGetValue(key, out var v) && v == "1";
        bool? GetNullableBool(string key) => deviceDataMap.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v == "1" : null;
        List<int> GetIntList(string key) => deviceDataMap.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)
            ? v.Split(':').Select(x => int.TryParse(x, out var n) ? n : 0).ToList()
            : new List<int>();

        // 3. Map and Assign directly to your model 

        var model = new ZkDeviceModel
        {
            DeviceInfo = new BiometricDevice
            {
                Id = payload.Info.DeviceInfo.Id,
                SN = payload.SN,
                TenantId = payload.Info.DeviceInfo.TenantId,
                DeviceName = GetStr("DeviceName") ?? "Unknown",
                MacAddress = GetStr("MAC") ?? "",
                IpAddress = GetStr("IPAddress") ?? "",
                Platform = GetStr("Platform") ?? "",
                OemVendor = GetStr("OEMVendor") ?? "",
                FwVersion = GetStr("FWVersion") ?? "",
                PushVersion = GetStr("PushVersion") ?? "",
                RegDeviceType = GetStr("RegDeviceType"),
                LanguageCode = GetInt("Language")
            },
            SystemCounters = new SystemCounters
            {
                SN = payload.SN,
                TenantId = payload.Info.DeviceInfo.TenantId,
                TransactionCount = GetInt("TransactionCount"),
                MaxAttLogCount = GetInt("MaxAttLogCount"),
                UserCount = GetInt("UserCount"),
                MaxUserCount = GetInt("MaxUserCount")
            },
            Biometrics = new Biometrics
            {
                SN = payload.SN,
                TenantId = payload.Info.DeviceInfo.TenantId,
                Fingerprint = new BiometricDetail { SN = payload.SN, TenantId = payload.Info.DeviceInfo.TenantId, Enabled = GetBool("FingerFunOn"), Version = GetStr("FPVersion") ?? "", Count = GetInt("FPCount"), MaxCount = GetNullableInt("MaxFingerCount") },
                Face = new BiometricDetail { SN = payload.SN, TenantId = payload.Info.DeviceInfo.TenantId, Enabled = GetBool("FaceFunOn"), Version = GetStr("FaceVersion") ?? "", Count = GetInt("FaceCount"), MaxCount = GetNullableInt("MaxFaceCount") },
                FingerVein = new BiometricDetail { SN = payload.SN, TenantId = payload.Info.DeviceInfo.TenantId, Enabled = GetBool("FvFunOn"), Version = GetStr("FvVersion") ?? "", Count = GetInt("FvCount"), MaxCount = GetNullableInt("MaxFvCount") },
                PalmVein = new BiometricDetail { SN = payload.SN, TenantId = payload.Info.DeviceInfo.TenantId, Enabled = GetBool("PvFunOn"), Version = GetStr("PvVersion") ?? "", Count = GetInt("PvCount"), MaxCount = GetNullableInt("MaxPvCount") }
            },
            PhotosAndMedia = new PhotosAndMedia
            {
                SN = payload.SN,
                TenantId = payload.Info.DeviceInfo.TenantId,
                PhotoFunctionEnabled = GetBool("PhotoFunOn"),
                UserPicUrlFunctionEnabled = GetBool("UserPicURLFunOn"),
                MaxUserPhotoCount = GetInt("MaxUserPhotoCount")
            },
            FeaturesAndProtocols = new FeaturesAndProtocols
            {
                SN = payload.SN,
                TenantId = payload.Info.DeviceInfo.TenantId,
                VisilightFun = GetBool("VisilightFun"),
                VisualIntercomFunOn = GetNullableBool("VisualIntercomFunOn"),
                VideoTid = GetStr("VideoTID"),
                VideoProtocol = GetStr("VideoProtocol"),
                SubcontractingUpgradeFunOn = GetBool("SubcontractingUpgradeFunOn"),
                QrCode = new QrCodeConfig
                {
                    SN = payload.SN,
                    TenantId = payload.Info.DeviceInfo.TenantId,
                    IsSupported = GetNullableBool("IsSupportQRcode"),
                    Enabled = GetNullableBool("QRCodeEnable"),
                    DecryptFunList = GetStr("QRCodeDecryptFunList")
                },
                ThermalAndMask = new ThermalAndMaskConfig
                {
                    SN = payload.SN,
                    TenantId = payload.Info.DeviceInfo.TenantId,
                    IrTempDetectionFunOn = GetNullableBool("IRTempDetectionFunOn"),
                    MaskDetectionFunOn = GetNullableBool("MaskDetectionFunOn")
                },
                ConfigSupport = new MultiBioSupport
                {
                    SN = payload.SN,
                    TenantId = payload.Info.DeviceInfo.TenantId,
                    DataSupport = GetIntList("MultiBioDataSupport"),
                    PhotoSupport = GetIntList("MultiBioPhotoSupport")
                }
            }
        };

        await _deviceService.UpdateDeviceInfo(model, token);

        // Verification Output
        //Console.WriteLine($"Successfully Parsed Device: {model.DeviceInfo.DeviceName}");
        //Console.WriteLine($"Vendor: {model.DeviceInfo.OemVendor}");
        //Console.WriteLine($"Face Bio - Enabled: {model.Biometrics.Face.Enabled}, Count: {model.Biometrics.Face.Count}/{model.Biometrics.Face.MaxCount}");
        //Console.WriteLine($"Palm Vein Bio Max Capacity: {(model.Biometrics.PalmVein.MaxCount?.ToString() ?? "Not Defined/Null")}");
        //Console.WriteLine($"First 3 Items in Bio Data Support array: {string.Join(", ", model.FeaturesAndProtocols.ConfigSupport.DataSupport.Take(3))}");

    }
}