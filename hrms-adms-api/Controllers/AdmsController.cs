using Asp.Versioning;
using Hrms.adms.Services;
using Hrms.adms.Services.Processors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Hrms.adms.Controllers;

[ApiController]
[Route("iclock")]
[ApiVersionNeutral]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
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
        config.Append("RegistryCode=0\r\n");
        config.Append("RegistryVer=1.0\r\n");
        config.Append("ATTLOGStamp=0\r\n");
        config.Append("ErrorDelay=30\r\n");
        config.Append("Delay=30\r\n");
        config.Append("TransTimes=00:00;23:59\r\n");
        config.Append("TransInterval=1\r\n");
        config.Append("TransFlag=TransData AttLog EnrollUser ChgUser EnrollFP ChgFP\r\n");
        config.Append("Realtime=1\r\n");
        config.Append("Encrypt=0\r\n");
        config.Append("TimeZone=8\r\n");

        return Content(config.ToString(), "text/plain");
    }

    // 2. HANDSHAKE
    [HttpGet("getrequest")]
    public async Task<IActionResult> GetRequest([FromQuery] string SN, CancellationToken token)
    {
        var deviceInfo = await _biometricDevice.FindSnAsync(SN, token);
        if (deviceInfo == null || deviceInfo.TenantId == Guid.Empty)
        {
            return Content("Unregistered", "text/plain");
        }

        _tenantProvider.SetTenantId(deviceInfo.TenantId);
        deviceInfo.State = "Online";
        _commandService.Context.BiometricDevices.Update(deviceInfo);
        await _commandService.CommitChangesAsync(token);

        // TODO: add SignalR here to publish state 
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
            if (string.IsNullOrWhiteSpace(item)) continue;

            // FIX: Parsing individual line item array instead of the raw body root
            var str = item.Split("&");
            if (str.Length > 1)
            {
                var resultTag = str[1].Split("=");
                var success = resultTag.Length > 1 && resultTag[1] == "0";

                if (success)
                {
                    var idTag = str[0].Split("=");
                    if (idTag.Length > 1 && Guid.TryParse(idTag[1], out Guid id))
                    {
                        await _commandService.DeleteAsync(id);
                    }
                }
            }
        }

        _commandService.CommitChanges();
        return Content("OK", "text/plain");
    }

    // 1. HANDSHAKE (GET)
    // Handles the initial operational handshake and configuration registry payload delivery
    [HttpGet("~/iclock/cdata")]
    public async Task<IActionResult> HandleCDataGet([FromQuery] string SN, CancellationToken token)
    {
        Log.Information("HandleCDataGet request from Device SN: {SN}", SN);

        var config = new StringBuilder();
        config.Append("RegistryCode=0\r\n");
        config.Append("RegistryVer=1.0\r\n");
        config.Append("ATTLOGStamp=0\r\n");
        //config.Append("OPERLOGStamp=0\r\n");
        //config.Append("ATTPHOTOStamp=0\r\n");
        config.Append("ErrorDelay=30\r\n");
        config.Append("Delay=30\r\n");
        config.Append("TransTimes=00:00;23:59\r\n");
        config.Append("TransInterval=1\r\n");
        config.Append("TransFlag=TransData AttLog  EnrollUser ChgUser EnrollFP ChgFP \r\n");
        //config.AppendLine("TransFlag=TransData AttLog OpLog EnrollUser ChgUser EnrollFP ChgFP UserPic");
        config.Append("Realtime=1\r\n");
        config.Append("Encrypt=0\r\n");
        config.Append("TimeZone=8\r\n");
        return Content(config.ToString(), "text/plain");
    }

    // 2. DATA RECEIVER (POST)
    // Handles incoming transactional records like ATTLOG tables
    [HttpPost("~/iclock/cdata")]
    public async Task<IActionResult> HandleCDataPost(CancellationToken token)
    {
        var sn = Request.Query["SN"].ToString();
        var table = Request.Query["table"].ToString();

        var deviceInfo = await _biometricDevice.FindSnAsync(sn, token);
        if (deviceInfo == null || deviceInfo.TenantId == Guid.Empty)
        {
            return NotFound();
        }

        _tenantProvider.SetTenantId(deviceInfo.TenantId);

        using var reader = new StreamReader(Request.Body);
        string rawBody = await reader.ReadToEndAsync();

        var processor = _serviceProvider.GetKeyedService<ICDataProcessor>(table);
        if (processor == null)
        {
            Log.Warning("SN: {sn} biometric table processor not found for target table: {table}", sn, table);
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