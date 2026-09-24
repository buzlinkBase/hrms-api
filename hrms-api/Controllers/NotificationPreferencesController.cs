using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Core.Services;
using Hrms.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    // Self-service only -- an employee manages their own email/push opt-out per application
    // type, nothing here reaches into another employee's preferences (unlike ApprovalsController's
    // admin-facing endpoints). See NotificationPreference's own doc comment for the opt-out model
    // (no row = both channels on).
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class NotificationPreferencesController : ControllerBase
    {
        private readonly NotificationPreferenceService _preferences;
        private readonly EmployeeService _employeeService;

        public NotificationPreferencesController(NotificationPreferenceService preferences, EmployeeService employeeService)
        {
            _preferences = preferences;
            _employeeService = employeeService;
        }

        private async Task<Guid> ResolveCallerEmployeeIdAsync(CancellationToken token) =>
            await _employeeService.ResolveEmployeeIdAsync(User.GetRequiredUserId(), User.GetUserClaim("email"), token)
            ?? throw new InvalidOperationException(
                "Your account isn't linked to an Employee record, so notification preferences can't be resolved.");

        // Returns every ApprovalApplicationType the caller has an explicit row for -- a category
        // with no row is implicitly both-channels-on and the frontend renders that default
        // itself, rather than this endpoint materializing a full row per category.
        [HttpGet("mine")]
        [ProducesResponseType(typeof(ResponseModel<List<NotificationPreferenceResponse>>), 200)]
        public async Task<IActionResult> GetMine(CancellationToken token)
        {
            var employeeId = await ResolveCallerEmployeeIdAsync(token);
            var preferences = await _preferences.GetForEmployeeAsync(employeeId, token);

            return Ok(preferences.Select(p => new NotificationPreferenceResponse
            {
                ApplicationType = p.ApplicationType,
                EmailEnabled = p.EmailEnabled,
                PushEnabled = p.PushEnabled,
            }).ToList());
        }

        [HttpPut("mine/{applicationType}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> UpdateMine(
            ApprovalApplicationType applicationType, [FromBody] UpdateNotificationPreferenceRequest body, CancellationToken token)
        {
            var employeeId = await ResolveCallerEmployeeIdAsync(token);
            await _preferences.UpsertAsync(employeeId, applicationType, body.EmailEnabled, body.PushEnabled, token);
            return Ok("success");
        }
    }

    public class NotificationPreferenceResponse
    {
        public ApprovalApplicationType ApplicationType { get; set; }
        public bool EmailEnabled { get; set; }
        public bool PushEnabled { get; set; }
    }

    public class UpdateNotificationPreferenceRequest
    {
        public bool EmailEnabled { get; set; } = true;
        public bool PushEnabled { get; set; } = true;
    }
}
