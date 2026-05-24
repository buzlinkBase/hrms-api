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
        var data = result.Split('\n');
        foreach (var item in data)
        {
            var str = result.Split("&");
            if (str.Length > 0)
            {
                var resultTag = str[1].Split("=");
                var success = resultTag[1] == "0";
                if (success)
                {
                    var IdTag = str[0].Split("=");
                    var Id = Guid.Parse(IdTag[1]);
                    _commandService.Delete(Id);
                }
            }
        }
        _commandService.CommitChanges();
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
 


//        // --- TEST CASE 5: SET_TIMEZONE ---
//        var setTzCmd = new DeviceCommandPayload
//        {
//            Id = 105,
//            Command = "SET_TIMEZONE",
//            Parameters = new() { { "option", "TimeZone" }, { "value", "420" } } // GMT+7 Example
//        };
//        PrintResult("SET_TIMEZONE", formatter.Format(setTzCmd));
  
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

 