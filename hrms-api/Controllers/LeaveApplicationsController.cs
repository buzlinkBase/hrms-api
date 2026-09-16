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

        [HttpPost]
        [RequirePermission("Leave:Create")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateLeaveApplication payload, CancellationToken token)
        {
            await _service.AddAsync(payload, token);
            await _service.CommitChangesAsync(token);
            return Ok();
        }

        // Bulk create-as-approved (writes ApprovalStatus.Approved directly) -- treated as a
        // bulk-create action; the auto-approve is a business-logic detail, not a second gate.
        [HttpPost("batch")]
        [RequirePermission("Leave:Create")]
        [ProducesResponseType(typeof(ResponseModel<List<LeaveApplicationModel>>), 200)]
        public async Task<IActionResult> PostBatch([FromBody] List<CreateLeaveApplication> payload, CancellationToken token)
        {
            var results = new List<LeaveApplicationModel?>();

            foreach (var item in payload)
            {
                var entity = _mapper.Map<LeaveApplication>(item);
                entity.ApprovalStatus = ApprovalStatus.Approved;
                results.Add(await _service.AddAsync(entity, token));
            }
            await _service.CommitChangesAsync(token);
            return Ok(results);
        }

        // This one PUT conflates a plain field edit and an approve/decline status change (the
        // frontend's changeStatus reuses this same endpoint) -- no way to split without a
        // backend code change, so either permission lets the call through.
        [HttpPut("{id}")]
        [RequirePermission("Leave:Edit", "Leave:Approve")]
        [ProducesResponseType(typeof(ResponseModel<LeaveApplicationModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateLeaveApplication payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;

            var approverEmployeeId = await _employeeService.ResolveEmployeeIdAsync(
                User.GetRequiredUserId(), User.GetUserClaim("email"), token);
            await _service.UpdateAsync(payload, token, approverEmployeeId, User.IsOwnerOrAdmin());

            return Ok(_mapper.Map<LeaveApplicationModel>(payload));
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
