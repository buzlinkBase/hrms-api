namespace Hrms.Api.Controllers.Adms;

public class DeviceInfo
{
    public string DeviceName { get; set; }
    public string MacAddress { get; set; }
    public int UserCount { get; set; }
    public string Firmware { get; set; }

    public static DeviceInfo Map(Dictionary<string, string> data)
    {
        return new DeviceInfo
        {
            DeviceName = data.GetValueOrDefault("~DeviceName"),
            MacAddress = data.GetValueOrDefault("MAC"),
            UserCount = int.Parse(data.GetValueOrDefault("UserCount", "0")),
            Firmware = data.GetValueOrDefault("FWVersion")
        };
    }
}
