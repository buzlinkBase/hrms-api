using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class OvertimeApplicationsController : ControllerBase
    {
        private readonly OvertimeApplicationService _service;
        private readonly EmployeeService _employeeService;
        private readonly IMapper _mapper;

        public OvertimeApplicationsController(OvertimeApplicationService service, EmployeeService employeeService, IMapper mapper)
        {
            _service = service;
            _employeeService = employeeService;
            _mapper = mapper;
        }

        [HttpGet]
        [RequirePermission("Overtime:View")]
        [ProducesResponseType(typeof(ResponseModel<List<OvertimeApplicationModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken token)
        {
            var data = await _service.FindAllAsync(token, from, to);
            return Ok(_mapper.Map<List<OvertimeApplicationModel>>(data));
        }

        [HttpGet("{id}")]
        [RequirePermission("Overtime:View")]
        [ProducesResponseType(typeof(ResponseModel<OvertimeApplicationModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<OvertimeApplicationModel>(data));
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
        [RequirePermission("Overtime:Create")]
        [ProducesResponseType(typeof(ResponseModel<OvertimeApplicationModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateOverTimeApplication payload, CancellationToken token)
        {
            var data = _mapper.Map<OverTimeApplication>(payload);
            data.ApprovalStatus = ApprovalStatus.ForApproval;
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<OvertimeApplicationModel>(data);
            return Ok(respModel);
        }

        [HttpPost("batch")]
        [RequirePermission("Overtime:Create")]
        [ProducesResponseType(typeof(ResponseModel<List<OvertimeApplicationModel>>), 200)]
        public async Task<IActionResult> PostBatch([FromBody] List<CreateOverTimeApplication> payload, CancellationToken token)
        {
            var entities = _mapper.Map<List<OverTimeApplication>>(payload);
            foreach (var entity in entities)
            {
                entity.ApprovalStatus = ApprovalStatus.ForApproval;
                await _service.AddAsync(entity, token);
            }
            return Ok(_mapper.Map<List<OvertimeApplicationModel>>(entities));
        }

        // Field edit only -- the service ignores any status on the payload. Approve/Decline below
        // are the only way to change it.
        [HttpPut("{id}")]
        [RequirePermission("Overtime:Edit")]
        [ProducesResponseType(typeof(ResponseModel<OvertimeApplicationModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateOvertimeApplication payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<OvertimeApplicationModel>(await _service.FineOneAsync(payload.Id, token)));
        }

        [HttpPost("{id}/approve")]
        [RequirePermission("Overtime:Approve")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Approve(Guid id, [FromBody] ApprovalActionRequest? body, CancellationToken token)
        {
            var (approverEmployeeId, hasOverride) = await ResolveApproverAsync(token);
            await _service.ApproveAsync(id, approverEmployeeId, hasOverride, body?.Note, token);
            return Ok();
        }

        [HttpPost("{id}/decline")]
        [RequirePermission("Overtime:Approve")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Decline(Guid id, [FromBody] ApprovalActionRequest? body, CancellationToken token)
        {
            var (approverEmployeeId, hasOverride) = await ResolveApproverAsync(token);
            await _service.DeclineAsync(id, approverEmployeeId, hasOverride, body?.Note, token);
            return Ok();
        }

        [HttpDelete("{id}")]
        [RequirePermission("Overtime:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
