using Onepunch.Common.Lib.Subscriptions;
using System.Security.Claims;
using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Core.Services;
using Hrms.Core.Services.WorkRotation;
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
[OperationsData]
[RequireFeature(EntitlementKeys.TimekeepingCore)]
public class WorkSchedulePlansController : ControllerBase
{
    private readonly WorkSchedulePlanService _service;
    private readonly EmployeeService _employeeService;
    private readonly ScheduleScopeService _scope;
    private readonly IMapper _mapper;
    public WorkSchedulePlansController(WorkSchedulePlanService service, EmployeeService employeeService, ScheduleScopeService scope, IMapper mapper)
    {
        _service = service;
        _employeeService = employeeService;
        _scope = scope;
        _mapper = mapper;
    }

    [HttpGet]
    [RequirePermission("Work Rotation:View")]
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
    // Reach = direct reports plus employees at the sites the supervisor was granted
    // (ScheduleScopeService). A supervisor with granted sites may also only send people to those
    // sites; one without any keeps the direct-reports-only behavior, any site allowed.
    internal async Task<IActionResult?> ValidateTeamScopeAsync(ClaimsPrincipal user, List<Guid> employeeIds, CancellationToken token, IEnumerable<Guid>? areaIds = null)
    {
        if (user.HasPermission("Work Rotation:Create")) return null;
        if (!user.HasPermission("Work Rotation:ManageOwnTeam")) return null;

        var myEmployeeId = await _employeeService.ResolveEmployeeIdAsync(user.GetRequiredUserId(), user.GetUserClaim("email"), token);
        if (myEmployeeId == null)
        {
            return Forbid();
        }

        var scope = await _scope.ResolveAsync(myEmployeeId.Value, token);
        if (!scope.CanSchedule(employeeIds) || !scope.CanUseAreas(areaIds ?? []))
        {
            return Forbid();
        }

        return null;
    }

    // What a team-scoped caller can schedule: the employees to list in the grid and the sites they
    // may send people to (empty = any site).
    [HttpGet("scope")]
    [ProducesResponseType(typeof(ResponseModel<ScheduleScopeModel>), 200)]
    public async Task<IActionResult> GetScope(CancellationToken token)
    {
        var myEmployeeId = await _employeeService.ResolveEmployeeIdAsync(User.GetRequiredUserId(), User.GetUserClaim("email"), token);
        if (myEmployeeId == null) return Forbid();

        var scope = await _scope.ResolveAsync(myEmployeeId.Value, token);
        return Ok(new ScheduleScopeModel(scope.Employees, scope.AllowedAreaIds.ToList()));
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(ResponseModel<List<WorkSchedulePlanModel>>), 200)]
    public async Task<IActionResult> PostBatch([FromBody] CreateWorkRotationPlanBatch payload, CancellationToken token)
    {
        var scopeViolation = await ValidateTeamScopeAsync(User, payload.EmployeeIds, token, payload.OperationAreaId.HasValue ? [payload.OperationAreaId.Value] : null);
        if (scopeViolation != null) return scopeViolation;

        if (payload.OperationAreaId is { } areaId && !await _service.AreaExistsAsync(areaId, token))
        {
            return BadRequest("The selected area does not exist.");
        }
        if (payload.ClientId is { } clientId && !await _service.ClientExistsAsync(clientId, token))
        {
            return BadRequest("The selected client does not exist.");
        }

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
                    OperationAreaId = payload.OperationAreaId,
                    ClientId = payload.ClientId,
                });
            }
        }
        await _service.AddRange(data, token);
        var respModel = _mapper.Map<List<WorkSchedulePlanModel>>(data);
        return Ok(respModel);
    }

    private const int MaxGridEntries = 5000;

    // What the Quick Schedule grid preloads. Open to anyone who can view or manage rotations; a
    // team-scoped caller must name the employees (the same scope check as saving) so they can't
    // read anyone else's roster through it.
    // A POST (not a GET) because the employee list can be hundreds of ids -- too long for a URL.
    [HttpPost("grid/query")]
    [ProducesResponseType(typeof(ResponseModel<List<WorkSchedulePlanModel>>), 200)]
    public async Task<IActionResult> QueryGrid([FromBody] WorkRotationGridFilter filter, CancellationToken token)
    {
        var (fromDate, toDate, employeeIds) = filter;
        if (!User.HasPermission("Work Rotation:View") && !User.HasPermission("Work Rotation:Create") && !User.HasPermission("Work Rotation:ManageOwnTeam"))
        {
            return Forbid();
        }
        if (toDate < fromDate) return BadRequest("The end date is before the start date.");

        var isTeamScoped = !User.HasPermission("Work Rotation:Create") && User.HasPermission("Work Rotation:ManageOwnTeam");
        if (isTeamScoped && (employeeIds == null || employeeIds.Count == 0))
        {
            return BadRequest("Select the employees to load.");
        }

        var scopeViolation = await ValidateTeamScopeAsync(User, employeeIds ?? [], token);
        if (scopeViolation != null) return scopeViolation;

        var data = await _service.FindGridAsync(filter, token);
        return Ok(data);
    }

    // Quick Schedule save: many employees x days, each with its own shift (or cleared). Scope is
    // the same rule as /batch, applied to every distinct employee in the payload.
    [HttpPost("grid")]
    [ProducesResponseType(typeof(ResponseModel<ApplyWorkRotationGridResult>), 200)]
    public async Task<IActionResult> PostGrid([FromBody] ApplyWorkRotationGrid payload, CancellationToken token)
    {
        if (payload.Entries == null || payload.Entries.Count == 0) return BadRequest("There is nothing to save.");
        if (payload.Entries.Count > MaxGridEntries) return BadRequest($"Too many changes at once (max {MaxGridEntries}). Save in smaller parts.");

        var employeeIds = payload.Entries.Select(e => e.EmployeeId).Distinct().ToList();
        var scopeViolation = await ValidateTeamScopeAsync(User, employeeIds, token, payload.Entries.Where(e => e.TimeShiftId.HasValue && e.OperationAreaId.HasValue).Select(e => e.OperationAreaId!.Value));
        if (scopeViolation != null) return scopeViolation;

        if (!await _service.AllEmployeesExistAsync(employeeIds, token))
            return BadRequest("One or more employees do not exist.");
        if (!await _service.AllShiftsExistAsync(payload.Entries.Where(e => e.TimeShiftId.HasValue).Select(e => e.TimeShiftId!.Value), token))
            return BadRequest("One or more time shifts do not exist.");

        foreach (var areaId in payload.Entries.Where(e => e.OperationAreaId.HasValue).Select(e => e.OperationAreaId!.Value).Distinct())
        {
            if (!await _service.AreaExistsAsync(areaId, token)) return BadRequest("The selected area does not exist.");
        }
        foreach (var clientId in payload.Entries.Where(e => e.ClientId.HasValue).Select(e => e.ClientId!.Value).Distinct())
        {
            if (!await _service.ClientExistsAsync(clientId, token)) return BadRequest("The selected client does not exist.");
        }

        var result = await _service.ApplyGridAsync(payload.Entries, token);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("Work Rotation:View")]
    [ProducesResponseType(typeof(ResponseModel<WorkSchedulePlanModel>), 200)]
    public async Task<IActionResult> GetOne(Guid id, CancellationToken token)
    {
        var data = await _service.GetModelAsync(id, token);
        return data == null ? NotFound() : Ok(data);
    }

    // Edit follows Create's rule (there is no separate Edit permission): unscoped for anyone with
    // Work Rotation:Create or neither permission, restricted to direct reports for ManageOwnTeam.
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ResponseModel<WorkSchedulePlanModel>), 200)]
    public async Task<IActionResult> Put(Guid id, [FromBody] UpdateWorkSchedulePlan payload, CancellationToken token)
    {
        var existing = await _service.GetModelAsync(id, token);
        if (existing == null) return NotFound();

        var scopeViolation = await ValidateTeamScopeAsync(User, [existing.EmployeeId], token, payload.OperationAreaId.HasValue ? [payload.OperationAreaId.Value] : null);
        if (scopeViolation != null) return scopeViolation;

        if (payload.OperationAreaId is { } areaId && !await _service.AreaExistsAsync(areaId, token))
        {
            return BadRequest("The selected area does not exist.");
        }
        if (payload.ClientId is { } clientId && !await _service.ClientExistsAsync(clientId, token))
        {
            return BadRequest("The selected client does not exist.");
        }

        payload.Id = id;
        await _service.UpdateAsync(payload, token);
        return Ok(await _service.GetModelAsync(id, token));
    }

    [HttpDelete("{id}")]
    [RequirePermission("Work Rotation:Delete")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token)
    {
        await _service.DeleteAsync(id, token);
        return Ok("Success");
    }

    [HttpDelete("batch")]
    [RequirePermission("Work Rotation:Delete")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    public async Task<IActionResult> DeleteBatch([FromQuery] string batchCode, CancellationToken token)
    {
        await _service.DeleteBatchAsync(batchCode, token);
        return Ok("Success");
    }
}
