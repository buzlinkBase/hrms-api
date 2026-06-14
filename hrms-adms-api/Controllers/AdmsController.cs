using Asp.Versioning;
using Hrms.adms.Services;
using Hrms.adms.Services.Processors;
using Microsoft.AspNetCore.Mvc;
using RTools_NTS.Util;
using System.Text;

namespace Hrms.adms.Controllers;

[ApiController]
[Route("iclock")]
[ApiVersionNeutral]
[ApiExplorerSettings(IgnoreApi =true)]
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
    //[HttpPost("registry")]
    //public IActionResult Registry([FromQuery] string SN)
    //{
    //    Log.Information("Registration request from Device SN: {SN}", SN);
    //    var config = new StringBuilder();
    //    config.AppendLine("TransFlag=1111000000");
    //    return Content(config.ToString(), "text/plain");
    //}

    [HttpPost("registry")]
    public IActionResult Registry([FromQuery] string SN)
    {

        //// Upsert device record
        //var device = await _context.Devices.FirstOrDefaultAsync(x => x.SerialNumber == SN, token);
        //if (device == null)
        //{
        //    device = new Device { SerialNumber = SN, RegisteredAt = DateTime.UtcNow };
        //    _context.Devices.Add(device);
        //    await _context.SaveChangesAsync(token);
        //    Log.Information("New device registered: {SN}", SN);
        //}
        Log.Information("Registration request from Device SN: {SN}", SN);
        var config = new StringBuilder();
        config.AppendLine("RegistryCode=0");
        config.AppendLine("RegistryVer=1.0");
        config.AppendLine($"ATTLOGStamp=0");        // ← sync attendance from beginning
        config.AppendLine($"OPERLOGStamp=0");       // ← sync operation logs
        config.AppendLine($"ATTPHOTOStamp=0");      // ← sync photos
        config.AppendLine("ErrorDelay=30");
        config.AppendLine("Delay=30");
        config.AppendLine("TransTimes=00:00;23:59");
        config.AppendLine("TransInterval=1");
        config.AppendLine("TransFlag=TransData AttLog OpLog EnrollUser ChgUser EnrollFP ChgFP UserPic");
        config.AppendLine("Realtime=1");
        config.AppendLine("Encrypt=0");
        config.AppendLine("TimeZone=8");            // ← set your timezone
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
    public async Task<IActionResult> HandleCDataGet([FromQuery] string SN)
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
 

 