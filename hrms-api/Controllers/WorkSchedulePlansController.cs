using System.Security.Claims;
using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class WorkSchedulePlansController : ControllerBase
{
    private readonly WorkSchedulePlanService _service;
    private readonly EmployeeService _employeeService;
    private readonly IMapper _mapper;
    public WorkSchedulePlansController(WorkSchedulePlanService service, EmployeeService employeeService, IMapper mapper)
    {
        _service = service;
        _employeeService = employeeService;
        _mapper = mapper;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ResponseModel<List<WorkSchedulePlanModel>>), 200)]
    public async Task<IActionResult> Get([FromQuery] WorkRotationPlanFilter filter, CancellationToken token)
    {
        var data = await _service.FindAllAsync(filter, token);
        return Ok(data);
    }

    //[HttpGet("range")]
    //[ProducesResponseType(typeof(ResponseModel<object>), 200)]
    //public async Task<IActionResult> Get([FromQuery] DateRequestPayload payload, CancellationToken token)
    //{
    //    var data = await _service.FindRange(payload, token);
    //    return Ok(data);
    //}
    //[HttpGet("{id}")]
    //[ProducesResponseType(typeof(ResponseModel<WorkSchedulePlanModel>), 200)]
    //public async Task<IActionResult> Get(Guid id, CancellationToken token)
    //{
    //    var data = await _service.FineOneAsync(id, token);
    //    return Ok(_mapper.Map<WorkSchedulePlanModel>(data));
    //}
    //[HttpPost]
    //[ProducesResponseType(typeof(ResponseModel<WorkSchedulePlanModel>), 200)]
    //public async Task<IActionResult> Post([FromBody] CreateWorkRotationPlan payload, CancellationToken token)
    //{
    //    var data = _mapper.Map<WorkSchedulePlan>(payload);
    //    await _service.AddAsync(data, token);
    //    var respModel = _mapper.Map<WorkSchedulePlanModel>(data);
    //    return Ok(respModel);
    //}

    // Work Rotation:Create (or no relevant permission at all -- the default for every role
    // except Owner today, since nothing has ever gated this endpoint) keeps today's exact
    // behavior: unscoped, any employeeId accepted. Only a caller holding ManageOwnTeam WITHOUT
    // Create -- e.g. a Supervisor Custom Role -- gets restricted to their own direct reports
    // (Employee.ManagerId). This is deliberately never a hard 403 for the "neither permission"
    // case, so introducing ManageOwnTeam can't retroactively lock out any current user; it's
    // purely additive/opt-in.
    // internal (not private) + an explicit ClaimsPrincipal param, rather than reading `User`
    // directly, so this authorization decision is unit-testable on its own -- exercising it
    // through the full PostBatch action would also require _service.AddRange's ExecuteDeleteAsync
    // to run against a real relational provider, which a plain mocked-repository test can't do.
    internal async Task<IActionResult?> ValidateTeamScopeAsync(ClaimsPrincipal user, List<Guid> employeeIds, CancellationToken token)
    {
        if (user.HasPermission("Work Rotation:Create")) return null;
        if (!user.HasPermission("Work Rotation:ManageOwnTeam")) return null;

        var myEmployeeId = await _employeeService.ResolveEmployeeIdAsync(user.GetRequiredUserId(), user.GetUserClaim("email"), token);
        if (myEmployeeId == null)
        {
            return Forbid();
        }

        var directReports = await _employeeService.Filter(new EmployeeFilter { ManagerId = myEmployeeId }, token);
        var allowedIds = directReports.Select(x => x.Id).ToHashSet();
        if (employeeIds.Any(id => !allowedIds.Contains(id)))
        {
            return Forbid();
        }

        return null;
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(ResponseModel<List<WorkSchedulePlanModel>>), 200)]
    public async Task<IActionResult> PostBatch([FromBody] CreateWorkRotationPlanBatch payload, CancellationToken token)
    {
        var scopeViolation = await ValidateTeamScopeAsync(User, payload.EmployeeIds, token);
        if (scopeViolation != null) return scopeViolation;

        var data = new List<WorkSchedulePlan>();
        foreach (var employeeId in payload.EmployeeIds)
        {
            foreach (var payrollDate in payload.PayrollDates)
            {
                data.Add(new WorkSchedulePlan
                {
                    EmployeeId = employeeId,
                    PayrollDate = payrollDate,
                    TimeShiftId = payload.TimeShiftId,
                });
            }
        }
        await _service.AddRange(data, token);
        var respModel = _mapper.Map<List<WorkSchedulePlanModel>>(data);
        return Ok(respModel);
    }

    //[HttpPut("{id}")]
    //[ProducesResponseType(typeof(ResponseModel<WorkSchedulePlanModel>), 200)]
    //public async Task<IActionResult> Put(Guid id, [FromBody] UpdateWorkSchedulePlan payload, CancellationToken token)
    //{
    //    payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
    //    await _service.UpdateAsync(payload, token);
    //    return Ok(_mapper.Map<WorkSchedulePlanModel>(payload));
    //}

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token)
    {
        await _service.DeleteAsync(id, token);
        return Ok("Success");
    }

    [HttpDelete("batch")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    public async Task<IActionResult> DeleteBatch([FromQuery] string batchCode, CancellationToken token)
    {
        await _service.DeleteBatchAsync(batchCode, token);
        return Ok("Success");
    }
}
