using Asp.Versioning;
using Hrms.Core.Services.ClientPortal;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

/// <summary>This company's connection to the Client Portal: where it is, the key it pushes with, and the last push.</summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class ClientPortalController : ControllerBase
{
    public const string ManagePermission = "clientportal:Client:Manage";

    private readonly ClientPortalSettingsService _settings;
    private readonly ClientPortalSyncRequestService _sync;

    public ClientPortalController(ClientPortalSettingsService settings, ClientPortalSyncRequestService sync)
    {
        _settings = settings;
        _sync = sync;
    }

    [HttpGet("settings")]
    [RequirePermission(ManagePermission)]
    [ProducesResponseType(typeof(ResponseModel<ClientPortalSettingsModel>), 200)]
    public async Task<IActionResult> GetSettings(CancellationToken token) => Ok(await _settings.GetAsync(token));

    /// <summary>The API key is write-only: leave it empty to keep the one already saved.</summary>
    [HttpPut("settings")]
    [RequirePermission(ManagePermission)]
    [ProducesResponseType(typeof(ResponseModel<ClientPortalSettingsModel>), 200)]
    public async Task<IActionResult> SaveSettings([FromBody] ClientPortalSettingsRequest request, CancellationToken token) =>
        Ok(await _settings.SaveAsync(request, token));

    /// <summary>Sends a posted DTR batch to the portal again (for example after the portal was down for a long time).</summary>
    [HttpPost("sync/{dtrBatchId:guid}")]
    [RequirePermission(ManagePermission)]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> Resync(Guid dtrBatchId, CancellationToken token)
    {
        await _sync.RequestAsync(dtrBatchId, token);
        return Ok("queued");
    }
}
