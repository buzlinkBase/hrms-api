using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Hrms.Api.Controllers.Adms;

[ApiController]
[Route("iclock")]
public class AdmsController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITenantProvider _tenantProvider;
    private readonly BiometricDeviceService _biometricDevice;
    private readonly ILogger<AdmsController> _logger;
    public AdmsController(
        IServiceProvider serviceProvider,
        ITenantProvider tenantProvider,
        BiometricDeviceService biometricDevice,
        ILogger<AdmsController> logger)
    {
        _serviceProvider = serviceProvider;
        _tenantProvider = tenantProvider;
        _biometricDevice = biometricDevice;
        _logger = logger;
    }

    // 1. REGISTRY
    [HttpPost("registry")]
    public IActionResult Registry([FromQuery] string SN)
    {
        _logger.LogInformation("Registration request from Device SN: {SN}", SN);
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
    public IActionResult GetRequest([FromQuery] string SN)
    {
        // You MUST provide a unique ID (e.g., from your DB). 
        // If the device has already processed '101', it won't run it again.
        var commandId = "1";
        //var commandText = $"C:{commandId}:DATA DELETE USERINFO PIN=2";
        //string commandText = $"C:{commandId}:DATA UPDATE USERINFO PIN=3\tName=Jordz\tPri=0"; 
        // Most firmware requires a trailing newline

        var commandText = new StringBuilder();
        //commandText.Append($"C:{commandId}:DATA UPDATE USERINFO ");
        //commandText.Append($"PIN=11\t");
        //commandText.Append($"Name=user11\t");
        //commandText.Append($"Pri=0\t");
        ////commandText.Append($"Passwd=1234\t");
        ////commandText.Append($"Card=4526");
        var response = commandText.Length > 0 ? commandText : commandText.Append("OK");
        return Content(response.ToString(), "text/plain");
    }

    [HttpPost("devicecmd")]
    public async Task<IActionResult> DeviceCmd([FromQuery] string SN)
    {
        using var reader = new StreamReader(Request.Body);
        string result = await reader.ReadToEndAsync();

        // Result will look like: "ID=101&Return=0" (0 means success)
        _logger.LogInformation("Device {SN} reported: {result}", SN, result);

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
        var biodevide = await _biometricDevice.FindSnAsync(sn, token);
        if (biodevide == null) return Ok();
        _tenantProvider.SetTenantId(biodevide.TenantId);
        using var reader = new StreamReader(Request.Body);
        string rawBody = await reader.ReadToEndAsync();

        var processor = _serviceProvider.GetKeyedService<ICDataProcessor>(table);
        if (processor == null)
        {
            _logger.LogInformation($"SN: {sn} bio table not manage table: {table}");
            return Content("Not Manage");
        }
        await processor.ProcessAsync(new BioPayload(sn, rawBody), token);
        return Content("OK", "text/plain");

    }


}

