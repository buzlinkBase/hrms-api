using Onepunch.Common.Lib.Subscriptions;
using Hrms.Api.Filters;
using Asp.Versioning;
using DTR.Core;
using Hrms.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

/// <summary>
/// My Shift Schedule: the caller's own shift and rest days per date, resolved by the DTR's own
/// schedule chains (see MyScheduleService). The employee is always the caller's, from their
/// token -- never taken from the request.
/// </summary>
[Route("api/v{version:apiVersion}/me/schedule")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
[RequireFeature(EntitlementKeys.PortalSelfService)]
[PortalPage(PortalPage.ShiftSchedule)]
public class MyScheduleController : ControllerBase
{
    private readonly EmployeeService _employeeService;
    private readonly MyScheduleService _scheduleService;

    public MyScheduleController(EmployeeService employeeService, MyScheduleService scheduleService)
    {
        _employeeService = employeeService;
        _scheduleService = scheduleService;
    }

    /// <param name="from">First date; defaults to today, local (Asia/Manila) time.</param>
    /// <param name="to">Last date; defaults to two weeks from <paramref name="from"/>, at most 62 days.</param>
    [HttpGet]
    [ProducesResponseType(typeof(ResponseModel<List<RosterReportModel>>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMySchedule([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken token)
    {
        var employeeId = await _employeeService.ResolveEmployeeIdAsync(
            User.GetRequiredUserId(), User.GetUserClaim("email"), token);
        if (employeeId == null) return NotFound();

        var today = MyScheduleService.LocalToday(DateTime.UtcNow);
        return Ok(await _scheduleService.GetAsync(employeeId.Value, from, to, today, token));
    }
}
