using Onepunch.Common.Lib.Subscriptions;
using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Core.Services;
using Hrms.Core.Services.WorkRotation;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

/// <summary>
/// Which project sites a supervisor may schedule (see SupervisorSiteGrant). Managed by whoever can
/// schedule anyone (Work Rotation:Create), never by the supervisor themselves.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
[OperationsData]
[RequireFeature(EntitlementKeys.TimekeepingCore)]
public class SupervisorSiteGrantsController : ControllerBase
{
    private readonly SupervisorSiteGrantService _service;
    private readonly WorkSchedulePlanService _plans;

    public SupervisorSiteGrantsController(SupervisorSiteGrantService service, WorkSchedulePlanService plans)
    {
        _service = service;
        _plans = plans;
    }

    [HttpGet("{employeeId:guid}")]
    [RequirePermission("Work Rotation:Create")]
    [ProducesResponseType(typeof(ResponseModel<List<Guid>>), 200)]
    public async Task<IActionResult> Get(Guid employeeId, CancellationToken token)
    {
        return Ok(await _service.GetAreaIdsAsync(employeeId, token));
    }

    [HttpPut("{employeeId:guid}")]
    [RequirePermission("Work Rotation:Create")]
    [ProducesResponseType(typeof(ResponseModel<List<Guid>>), 200)]
    public async Task<IActionResult> Put(Guid employeeId, [FromBody] SetSupervisorSiteGrants payload, CancellationToken token)
    {
        var areaIds = (payload.AreaIds ?? []).Distinct().ToList();

        if (!await _plans.AllEmployeesExistAsync([employeeId], token))
            return BadRequest("The employee does not exist.");
        foreach (var areaId in areaIds)
        {
            if (!await _plans.AreaExistsAsync(areaId, token))
                return BadRequest("One or more project sites do not exist.");
        }

        await _service.SetAsync(employeeId, areaIds, token);
        return Ok(areaIds);
    }
}
