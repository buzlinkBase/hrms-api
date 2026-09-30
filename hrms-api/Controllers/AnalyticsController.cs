using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Core.Services.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

/// <summary>
/// Analytics: trends and breakdowns per area, each behind its own permission. Everything is read
/// through the normal company-scoped, confidentiality-filtered queries, so confidential employees
/// only count for callers cleared to see them. A range is capped at 24 months.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<AnalyticsResult>), 200)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
public class AnalyticsController : ControllerBase
{
    private readonly WorkforceAnalyticsService _workforce;
    private readonly AttendanceAnalyticsService _attendance;
    private readonly PayrollAnalyticsService _payroll;
    private readonly RequestAnalyticsService _requests;

    public AnalyticsController(
        WorkforceAnalyticsService workforce,
        AttendanceAnalyticsService attendance,
        PayrollAnalyticsService payroll,
        RequestAnalyticsService requests)
    {
        _workforce = workforce;
        _attendance = attendance;
        _payroll = payroll;
        _requests = requests;
    }

    [HttpGet("workforce")]
    [RequirePermission("Workforce Analytics:View")]
    public async Task<IActionResult> Workforce([FromQuery] AnalyticsFilter filter, CancellationToken token) =>
        Ok(await _workforce.GetAsync(filter, token));

    [HttpGet("attendance")]
    [RequirePermission("Attendance Analytics:View")]
    public async Task<IActionResult> Attendance([FromQuery] AnalyticsFilter filter, CancellationToken token) =>
        Ok(await _attendance.GetAsync(filter, token));

    [HttpGet("payroll")]
    [RequirePermission("Payroll Analytics:View")]
    public async Task<IActionResult> Payroll([FromQuery] AnalyticsFilter filter, CancellationToken token) =>
        Ok(await _payroll.GetAsync(filter, token));

    [HttpGet("requests")]
    [RequirePermission("Request Analytics:View")]
    public async Task<IActionResult> Requests([FromQuery] AnalyticsFilter filter, CancellationToken token) =>
        Ok(await _requests.GetAsync(filter, token));
}
