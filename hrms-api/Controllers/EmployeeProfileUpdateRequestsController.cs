using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Core.Services;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class EmployeeProfileUpdateRequestsController : ControllerBase
    {
        private readonly EmployeeProfileUpdateRequestService _service;
        private readonly EmployeeService _employeeService;

        public EmployeeProfileUpdateRequestsController(EmployeeProfileUpdateRequestService service, EmployeeService employeeService)
        {
            _service = service;
            _employeeService = employeeService;
        }

        private async Task<(Guid? ApproverEmployeeId, bool HasOverride)> ResolveApproverAsync(CancellationToken token)
        {
            var approverEmployeeId = await _employeeService.ResolveEmployeeIdAsync(
                User.GetRequiredUserId(), User.GetUserClaim("email"), token);
            return (approverEmployeeId, User.IsOwnerOrAdmin());
        }

        [HttpGet]
        [RequirePermission("Profile Update:View")]
        [ProducesResponseType(typeof(ResponseModel<List<EmployeeProfileUpdateRequestModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] Guid? employeeId, CancellationToken token)
        {
            var data = await _service.FindAllAsync(employeeId, token);
            return Ok(data);
        }

        [HttpGet("{id}")]
        [RequirePermission("Profile Update:View")]
        [ProducesResponseType(typeof(ResponseModel<EmployeeProfileUpdateRequestModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FindOneWithCurrentValuesAsync(id, token)
                ?? throw new NotFoundException("Record not found");
            return Ok(data);
        }

        // Returns 409 with the live Employee.UpdatedAt when the request's snapshot is stale
        // instead of silently overwriting a more recent direct edit -- see
        // EmployeeProfileUpdateRequest.EmployeeSnapshotUpdatedAt. Resubmit with ForceApply=true
        // once the approver has reviewed the conflict.
        [HttpPost("{id}/approve")]
        [RequirePermission("Profile Update:Approve")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        [ProducesResponseType(typeof(ResponseModel<object>), 409)]
        public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveProfileUpdateRequest? body, CancellationToken token)
        {
            var (approverEmployeeId, hasOverride) = await ResolveApproverAsync(token);
            var result = await _service.ApproveAsync(id, approverEmployeeId, hasOverride, body?.Note, body?.ForceApply ?? false, token);
            if (result.HasConflict)
                return Conflict(new { message = "This employee's record was changed since this request was submitted.", employeeUpdatedAt = result.EmployeeUpdatedAt });
            return Ok();
        }

        [HttpPost("{id}/decline")]
        [RequirePermission("Profile Update:Approve")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Decline(Guid id, [FromBody] ApprovalActionRequest? body, CancellationToken token)
        {
            var (approverEmployeeId, hasOverride) = await ResolveApproverAsync(token);
            await _service.DeclineAsync(id, approverEmployeeId, hasOverride, body?.Note, token);
            return Ok();
        }
    }
}
