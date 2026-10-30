using Asp.Versioning;
using Hrms.Core.Services.Compliance;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

// Company Settings > Acknowledged deviations: every time an admin confirmed a setting that differs
// from the statutory (DOLE) defaults -- who, when, the setting, the default and the chosen value.
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class ComplianceAcknowledgmentsController : ControllerBase
{
    private readonly ComplianceAcknowledgmentService _service;

    public ComplianceAcknowledgmentsController(ComplianceAcknowledgmentService service) => _service = service;

    [HttpGet]
    [RequirePermission("Organization Setup:View")]
    [ProducesResponseType(typeof(ResponseModel<ComplianceAcknowledgmentPage>), 200)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken token = default) =>
        Ok(await _service.ListAsync(page, pageSize, token));
}
