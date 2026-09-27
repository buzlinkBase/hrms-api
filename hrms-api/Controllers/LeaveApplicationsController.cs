using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Domain.Entities;
using Mapster;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class LeaveApplicationsController : ControllerBase
    {
        private readonly LeaveApplicationService _service;
        private readonly EmployeeService _employeeService;
        private readonly IMapper _mapper;

        public LeaveApplicationsController(LeaveApplicationService service, EmployeeService employeeService, IMapper mapper)
        {
            _service = service;
            _employeeService = employeeService;
            _mapper = mapper;
        }

        [HttpGet("range")]
        [RequirePermission("Leave:View")]
        [ProducesResponseType(typeof(ResponseModel<List<LeaveApplicationModel>>), 200)]
        public async Task<IActionResult> Range([FromQuery] DateEmployeeRequestPayload payload, CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(data);
        }

        [HttpGet]
        [RequirePermission("Leave:View")]
        [ProducesResponseType(typeof(ResponseModel<List<LeaveApplicationModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken token)
        {
            var data = await _service.FindAllAsync(token, from, to);
            return Ok(data);
        }

        [HttpGet("{id}")]
        [RequirePermission("Leave:View")]
        [ProducesResponseType(typeof(ResponseModel<LeaveApplicationModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(data);
        }

        private async Task<(Guid? ApproverEmployeeId, bool HasOverride)> ResolveApproverAsync(CancellationToken token)
        {
            var approverEmployeeId = await _employeeService.ResolveEmployeeIdAsync(
                User.GetRequiredUserId(), User.GetUserClaim("email"), token);
            return (approverEmployeeId, User.IsOwnerOrAdmin());
        }

        // Every filing starts ForApproval and goes through the approval workflow -- the filer
        // never chooses the status, so no one can skip the approvers.
        [HttpPost]
        [RequirePermission("Leave:Create")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateLeaveApplication payload, CancellationToken token)
        {
            payload.ApprovalStatus = ApprovalStatus.ForApproval;
            await _service.AddAsync(payload, token);
            return Ok();
        }

        // Same path as a single filing per row (policy checks, approval workflow, grant events).
        [HttpPost("batch")]
        [RequirePermission("Leave:Create")]
        [ProducesResponseType(typeof(ResponseModel<List<LeaveApplicationModel>>), 200)]
        public async Task<IActionResult> PostBatch([FromBody] List<CreateLeaveApplication> payload, CancellationToken token)
        {
            var results = new List<LeaveApplicationModel?>();
            foreach (var item in payload)
            {
                item.ApprovalStatus = ApprovalStatus.ForApproval;
                results.Add(await _service.AddAsync(item, token));
            }
            return Ok(results);
        }

        // Field edit only -- the service ignores any status on the payload. Approve/Decline below
        // are the only way to change it.
        [HttpPut("{id}")]
        [RequirePermission("Leave:Edit")]
        [ProducesResponseType(typeof(ResponseModel<LeaveApplicationModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateLeaveApplication payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(await _service.FineOneAsync(payload.Id, token));
        }

        [HttpPost("{id}/approve")]
        [RequirePermission("Leave:Approve")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Approve(Guid id, [FromBody] ApprovalActionRequest? body, CancellationToken token)
        {
            var (approverEmployeeId, hasOverride) = await ResolveApproverAsync(token);
            await _service.ApproveAsync(id, approverEmployeeId, hasOverride, body?.Note, token);
            return Ok();
        }

        [HttpPost("{id}/decline")]
        [RequirePermission("Leave:Approve")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Decline(Guid id, [FromBody] ApprovalActionRequest? body, CancellationToken token)
        {
            var (approverEmployeeId, hasOverride) = await ResolveApproverAsync(token);
            await _service.DeclineAsync(id, approverEmployeeId, hasOverride, body?.Note, token);
            return Ok();
        }

        [HttpDelete("{id}")]
        [RequirePermission("Leave:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            await _service.CommitChangesAsync(token);
            return Ok();
        }

        [HttpPut("{id}/reimbursement")]
        [RequirePermission("Leave:Edit")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> UpdateReimbursement(Guid id, [FromBody] UpdateReimbursementStatus payload, CancellationToken token)
        {
            await _service.UpdateReimbursementStatusAsync(id, payload, token);
            return Ok();
        }
    }
}
