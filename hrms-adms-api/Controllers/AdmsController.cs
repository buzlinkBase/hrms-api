using Hrms.adms.Services;
using Hrms.adms.Services.Processors;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Hrms.adms.Controllers;

[ApiController]
[Route("iclock")]
public class AdmsController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITenantProvider _tenantProvider;
    private readonly DeviceService _biometricDevice;
    private readonly CommandService _commandService;
    public AdmsController(
        IServiceProvider serviceProvider,
        ITenantProvider tenantProvider,
        DeviceService biometricDevice,
        CommandService commandService,
        ILogger<AdmsController> logger)
    {
        _serviceProvider = serviceProvider;
        _tenantProvider = tenantProvider;
        _biometricDevice = biometricDevice;
        _commandService = commandService;
    }

    // 1. REGISTRY
    [HttpPost("registry")]
    public IActionResult Registry([FromQuery] string SN)
    {
        Log.Information("Registration request from Device SN: {SN}", SN);
        var config = new StringBuilder();
        config.AppendLine("RegistryCode=0");
        config.AppendLine("RegistryVer=1.0");
        config.AppendLine("Delay=30");
        config.AppendLine("TransTimes=00:00;23:59");
        config.AppendLine("TransInterval=1");
        config.AppendLine("TransFlag=1111000000");
        config.AppendLine("Realtime=1");
        config.AppendLine("Encrypt=0");
        return Content(config.ToString(), "text/plain");
    }

    // 2. HANDSHAKE
    [HttpGet("getrequest")]
    public async Task<IActionResult> GetRequest([FromQuery] string SN)
    {
        var data = await _commandService.GetAllCommandsAsync(SN);
        if (data == null || !data.Any())
        {
            return Content("OK", "text/plain");
        }
        var responseText = string.Join("\n", data.Select(item => item.Commands));
        if (string.IsNullOrWhiteSpace(responseText))
        {
            responseText = "OK";
        }
        return Content(responseText, "text/plain");
    }

    [HttpPost("devicecmd")]
    public async Task<IActionResult> DeviceCmd([FromQuery] string SN)
    {
        using var reader = new StreamReader(Request.Body);
        string result = await reader.ReadToEndAsync();
        //var commands = JsonSerializer.Deserialize<DeviceResult>(result);
        //// Result will look like: "ID=101&Return=0" (0 means success)
        ////_logger.LogInformation("Device {SN} reported: {result}", SN, result);
        //var str = result.Split("&");
        //if (str.Length > 0)
        //{
        //    var success = str[1].Split("=")[1] == "0";
        //    if (success)
        //    {
        //        var Id = Guid.Parse(str[0].Split("=")[0]);
        //        _commandService.Delete(Id);
        //        Log.Logger.Information("devicecmd success {0}", result);
        //        return Content("OK", "text/plain");
        //    }
        //}
        //Log.Logger.Warning("devicecmd fail {0}", result);
        //return Content("Failure", "text/plain");
        return Content("OK", "text/plain");
    }

    // 1. HANDSHAKE (GET)
    // Handles the "Is the server there?" requests
    [HttpGet("~/iclock/cdata")]
    public IActionResult HandleCDataGet([FromQuery] string? SN)
    {
        return Content("OK", "text/plain");
    }

    // 2. DATA RECEIVER (POST)
    // Handles the actual attendance logs and data pushes
    [HttpPost("~/iclock/cdata")]
    public async Task<IActionResult> HandleCDataPost(CancellationToken token)
    {
        var req = Request.Query;
        var sn = Request.Query["SN"].ToString();
        var table = Request.Query["table"].ToString();
        var deviceInfo = await _biometricDevice.FindSnAsync(sn, token);
        if (deviceInfo == null || Guid.Empty == deviceInfo.TenantId || deviceInfo.TenantId == Guid.Empty) return NotFound();
        _tenantProvider.SetTenantId(deviceInfo.TenantId);

        using var reader = new StreamReader(Request.Body);
        string rawBody = await reader.ReadToEndAsync();
        var processor = _serviceProvider.GetKeyedService<ICDataProcessor>(table);
        if (processor == null)
        {
            Log.Warning($"SN: {sn} bio table not manage table: {table}");
            return Content("Not Manage");
        }
        await processor.ProcessAsync(new BioPayload(sn, rawBody, new Models.DTO.ZkDeviceModel { DeviceInfo = deviceInfo }), token);
        return Content("OK", "text/plain");
    }
}
public class DeviceResult
{
    public int Id { get; set; }
    public int Return { get; set; }
    public string CMD { get; set; }

}

//sample commands
//class Program
//{
//    static void Main(string[] args)
//    {
//        // 1. Initialize dependencies
//        ISystemClockService clockService = new ClockNormalize();
//        var formatter = new ZKTecoCommandFormatter(clockService);

//        Console.WriteLine("================ ZKTECO COMMAND GENERATION ================\n");

//        // --- TEST CASE 1: RESTART / REBOOT ---
//        var rebootCmd = new DeviceCommandPayload { Id = 101, Command = "REBOOT" };
//        PrintResult("REBOOT", formatter.Format(rebootCmd));

//        // --- TEST CASE 2: CLEAR_LOGS ---
//        var clearLogsCmd = new DeviceCommandPayload { Id = 102, Command = "CLEAR_LOGS" };
//        PrintResult("CLEAR_LOGS", formatter.Format(clearLogsCmd));

//        // --- TEST CASE 3: SET_TIME (With Timestamp) ---
//        var setTimeCmd = new DeviceCommandPayload
//        {
//            Id = 103,
//            Command = "SET_TIME",
//            Parameters = new() { { "timestamp", DateTime.Now } }
//        };
//        PrintResult("SET_TIME (Timestamp)", formatter.Format(setTimeCmd));

//        // --- TEST CASE 4: SET_TIME (With Option Override) ---
//        var setTimeOptCmd = new DeviceCommandPayload
//        {
//            Id = 104,
//            Command = "SET_TIME",
//            Parameters = new() { { "option", "AutoServerTime" }, { "value", "1" } }
//        };
//        PrintResult("SET_TIME (Option)", formatter.Format(setTimeOptCmd));

//        // --- TEST CASE 5: SET_TIMEZONE ---
//        var setTzCmd = new DeviceCommandPayload
//        {
//            Id = 105,
//            Command = "SET_TIMEZONE",
//            Parameters = new() { { "option", "TimeZone" }, { "value", "420" } } // GMT+7 Example
//        };
//        PrintResult("SET_TIMEZONE", formatter.Format(setTzCmd));

//        // --- TEST CASE 6: ENABLE_ATTENDANCE ---
//        var enableAttCmd = new DeviceCommandPayload { Id = 106, Command = "ENABLE_ATTENDANCE" };
//        PrintResult("ENABLE_ATTENDANCE", formatter.Format(enableAttCmd));

//        // --- TEST CASE 7: DISABLE_ATTENDANCE ---
//        var disableAttCmd = new DeviceCommandPayload { Id = 107, Command = "DISABLE_ATTENDANCE" };
//        PrintResult("DISABLE_ATTENDANCE", formatter.Format(disableAttCmd));

//        // --- TEST CASE 8: SET_ATTENDANCE_MODE ---
//        var attModeCmd = new DeviceCommandPayload
//        {
//            Id = 108,
//            Command = "SET_ATTENDANCE_MODE",
//            Parameters = new() { { "option", "AttFmt" }, { "value", "1" } }
//        };
//        PrintResult("SET_ATTENDANCE_MODE", formatter.Format(attModeCmd));

//        // --- TEST CASE 9: SYNC_EMPLOYEES (Add/Update User) ---
//        var syncEmpCmd = new DeviceCommandPayload
//        {
//            Id = 109,
//            Command = "SYNC_EMPLOYEES",
//            Parameters = new()
//            {
//                { "employee_code", "9988" }, // Falling back to employee_code
//                { "name", "John Doe" },
//                { "card_no", "12345678" },
//                { "privilege", 0 },
//                { "password", "4321" },
//                { "group_code", "1" }
//            }
//        };
//        PrintResult("SYNC_EMPLOYEES (Update)", formatter.Format(syncEmpCmd));

//        // --- TEST CASE 10: SYNC_EMPLOYEES (Query Single User) ---
//        var queryEmpCmd = new DeviceCommandPayload
//        {
//            Id = 110,
//            Command = "SYNC_EMPLOYEES",
//            Parameters = new() { { "action", "query" }, { "pin", "9988" } }
//        };
//        PrintResult("SYNC_EMPLOYEES (Query)", formatter.Format(queryEmpCmd));

//        // --- TEST CASE 11: SYNC_BIOMETRICS (Fingerprint with spaced template) ---
//        // Mimicking base64 template with spaces. Regex.Replace will strip these out.
//        string fakeFpBase64 = "U09NRSBGSU5HRVJQUklOVCBEQVRBIEhFUkU=";
//        var syncFpCmd = new DeviceCommandPayload
//        {
//            Id = 111,
//            Command = "SYNC_BIOMETRICS",
//            Parameters = new()
//            {
//                { "pin", "1002" },
//                { "biometric_type", "fingerprint" },
//                { "template_index", "2" },
//                { "template_data", fakeFpBase64 },
//                { "is_valid", "true" }
//            }
//        };
//        PrintResult("SYNC_BIOMETRICS (Fingerprint)", formatter.Format(syncFpCmd));

//        // --- TEST CASE 12: SYNC_BIOMETRICS (Face Template) ---
//        string fakeFaceBase64 = "RkFDRSBURU1QTEFURSBXSVRIU1BBQ0VTIElOIEJBU0U2NA==";
//        var syncFaceCmd = new DeviceCommandPayload
//        {
//            Id = 112,
//            Command = "SYNC_BIOMETRICS",
//            Parameters = new()
//            {
//                { "pin", "1002" },
//                { "biometric_type", "face" },
//                { "template_index", "50" },
//                { "template_data", fakeFaceBase64 },
//                { "face_upload_prefixed", false }
//            }
//        };
//        PrintResult("SYNC_BIOMETRICS (Face)", formatter.Format(syncFaceCmd));

//        // --- TEST CASE 13: SYNC_BIOMETRICS (User Photo) ---
//        var syncPhotoCmd = new DeviceCommandPayload
//        {
//            Id = 113,
//            Command = "SYNC_BIOMETRICS",
//            Parameters = new()
//            {
//                { "pin", "1002" },
//                { "biometric_type", "photo" },
//                { "file_name", "1002.jpg" },
//                { "template_data", "IMAGE_RAW_OR_BASE64_STREAM" }
//            }
//        };
//        PrintResult("SYNC_BIOMETRICS (Photo)", formatter.Format(syncPhotoCmd));

//        // --- TEST CASE 14: ENROLL_FINGERPRINT ---
//        var enrollFpCmd = new DeviceCommandPayload
//        {
//            Id = 114,
//            Command = "ENROLL_FINGERPRINT",
//            Parameters = new()
//            {
//                { "pin", "1005" },
//                { "fid", 1 }, // Finger ID (Index 0-9)
//                { "retry", 3 },
//                { "overwrite", true }
//            }
//        };
//        PrintResult("ENROLL_FINGERPRINT", formatter.Format(enrollFpCmd));

//        // --- TEST CASE 15: ENROLL_FACE ---
//        var enrollFaceCmd = new DeviceCommandPayload
//        {
//            Id = 115,
//            Command = "ENROLL_FACE",
//            Parameters = new()
//            {
//                { "pin", "1005" },
//                { "card_no", "87654321" },
//                { "retry", 5 },
//                { "type", 2 }
//            }
//        };
//        PrintResult("ENROLL_FACE", formatter.Format(enrollFaceCmd));

//        // --- TEST CASE 16: PULL_EMPLOYEES (All or Single) ---
//        var pullEmpCmd = new DeviceCommandPayload
//        {
//            Id = 116,
//            Command = "PULL_EMPLOYEES",
//            Parameters = new() { { "pin", "1005" } }
//        };
//        PrintResult("PULL_EMPLOYEES", formatter.Format(pullEmpCmd));

//        // --- TEST CASE 17: PULL_ATTENDANCE (Filtered Logs) ---
//        var pullAttCmd = new DeviceCommandPayload
//        {
//            Id = 117,
//            Command = "PULL_ATTENDANCE",
//            Parameters = new()
//            {
//                { "start_time", "2026-05-19 00:00:00" },
//                { "end_time", "2026-05-19 23:59:59" }
//            }
//        };
//        PrintResult("PULL_ATTENDANCE", formatter.Format(pullAttCmd));

//        // --- TEST CASE 18: RM_ADMIN_PRIVILEGE ---
//        var rmAdminCmd = new DeviceCommandPayload { Id = 118, Command = "RM_ADMIN_PRIVILEGE" };
//        PrintResult("RM_ADMIN_PRIVILEGE", formatter.Format(rmAdminCmd));

//        // --- TEST CASE 19: CommandPayload Direct Bypass ---
//        var rawPayloadCmd = new DeviceCommandPayload
//        {
//            Id = 119,
//            CommandPayload = "CUSTOM:RAW:STRING:NO:ID:REQUIRED"
//        };
//        PrintResult("CommandPayload (Direct)", formatter.Format(rawPayloadCmd));

//        // --- TEST CASE 20: Fallback Raw Command ---
//        var fallbackCmd = new DeviceCommandPayload { Id = 120, Command = "CUSTOM_UNKNOWN_OP" };
//        PrintResult("Fallback Command", formatter.Format(fallbackCmd));
//    }

//    private static void PrintResult(string caseName, string output)
//    {
//        Console.ForegroundColor = ConsoleColor.Cyan;
//        Console.Write($"{caseName,-30}: ");
//        Console.ForegroundColor = ConsoleColor.White;
//        Console.WriteLine(output);
//    }
//}