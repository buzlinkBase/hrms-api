using Microsoft.AspNetCore.Mvc;
using System.Text;
using Hrms.Domain.Entities;

namespace Hrms.Api.Controllers.Adms;

[ApiController]
[Route("iclock")] // Standard route configuration
public class AdmsController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdmsController> _logger;
    public AdmsController(
        IServiceProvider serviceProvider,
        ILogger<AdmsController> logger)
    {
        _serviceProvider = serviceProvider;
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
        _logger.LogInformation("Heartbeat received from SN: {SN}", SN);
        return Content("OK", "text/plain");
    }

    // 1. HANDSHAKE (GET)
    // Handles the "Is the server there?" requests
    [HttpGet("~/iclock/cdata")]
    public IActionResult HandleCDataGet([FromQuery] string? SN)
    {
        _logger.LogInformation("CData GET handshake received from SN: {SN}", SN);
        // The device expects "OK" to acknowledge it's connected
        return Content("OK", "text/plain");
    }

    // 2. DATA RECEIVER (POST)
    // Handles the actual attendance logs and data pushes
    [HttpPost("~/iclock/cdata")]
    public async Task<IActionResult> HandleCDataPost(CancellationToken token)
    {
        var req = Request.Query;
        // 1. Read the raw body
        var sn = Request.Query["SN"].ToString();
        var table = Request.Query["table"].ToString();
        using var reader = new StreamReader(Request.Body);
        string rawBody = await reader.ReadToEndAsync();
        _logger.LogInformation("CData POST received. Table: {table}, Raw Body Length: {len}", table, rawBody.Length);
        // 2. Process using your Factory
        var processor = _serviceProvider.GetRequiredKeyedService<ICDataProcessor>(table);
        if (processor == null)
        {
            return Content("Not Manage");
        }
        await processor.ProcessAsync(sn, rawBody, token);
        // 3. Return the acknowledgment
        // Note: If "OKx" is working for your specific model, keep it. 
        // If you run into infinite loop issues, switch this back to just "OK".
        return Content("OKx", "text/plain");
    }

    // 4. COMMAND LOGGING
    [HttpPost("devicecmd")]
    public IActionResult DeviceCmd([FromQuery] string SN)
    {
        _logger.LogInformation("Device SN: {SN} confirmed command execution.", SN);
        return Content("OK", "text/plain");
    }
}

public class UserRegistration
{
    public string UserPin { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Password { get; set; } = string.Empty;
    public string Card { get; set; } = string.Empty;
}

