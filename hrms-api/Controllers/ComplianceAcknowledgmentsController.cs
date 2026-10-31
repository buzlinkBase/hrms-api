using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Core.Services.Compliance;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

// Company Settings > Acknowledged deviations: every time an admin confirmed a setting that differs
// from the statutory (DOLE) defaults -- who, when, the setting, the default and the chosen value --
// plus the deviations in effect that nobody has confirmed yet.
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class ComplianceAcknowledgmentsController : ControllerBase
{
    private readonly ComplianceAcknowledgmentService _service;
    private readonly OutstandingDeviationService _outstanding;

    public ComplianceAcknowledgmentsController(ComplianceAcknowledgmentService service, OutstandingDeviationService outstanding)
    {
        _service = service;
        _outstanding = outstanding;
    }

    [HttpGet]
    [RequirePermission("Organization Setup:View")]
    [ProducesResponseType(typeof(ResponseModel<ComplianceAcknowledgmentPage>), 200)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken token = default) =>
        Ok(await _service.ListAsync(page, pageSize, token));

    // Settings in effect that differ from the DOLE defaults with no acknowledgment on record, and
    // whether the Terms are live (while they aren't, nothing is asked for or recorded).
    [HttpGet("outstanding")]
    [RequirePermission("Organization Setup:View")]
    [ProducesResponseType(typeof(ResponseModel<OutstandingDeviationsResult>), 200)]
    public async Task<IActionResult> Outstanding(CancellationToken token = default) =>
        Ok(await _outstanding.ListAsync(token));

    [HttpPost("outstanding/confirm")]
    [RequirePermission("Organization Setup:Edit")]
    [ProducesResponseType(typeof(ResponseModel<int>), 200)]
    public async Task<IActionResult> ConfirmOutstanding(CancellationToken token = default)
    {
        var confirmed = await _outstanding.ConfirmAllAsync(HttpContext.ToComplianceActor(), token);
        await _outstanding.CommitChangesAsync(token);
        return Ok(confirmed);
    }
}
