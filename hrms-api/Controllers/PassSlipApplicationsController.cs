using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Domain.Entities;
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
    public class PassSlipApplicationsController : ControllerBase
    {
        private readonly PassSlipApplicationService _service;
        private readonly EmployeeService _employeeService;
        private readonly IMapper _mapper;

        public PassSlipApplicationsController(PassSlipApplicationService service, EmployeeService employeeService, IMapper mapper)
        {
            _service = service;
            _employeeService = employeeService;
            _mapper = mapper;
        }

        private async Task<(Guid? ApproverEmployeeId, bool HasOverride)> ResolveApproverAsync(CancellationToken token)
        {
            var approverEmployeeId = await _employeeService.ResolveEmployeeIdAsync(
                User.GetRequiredUserId(), User.GetUserClaim("email"), token);
            return (approverEmployeeId, User.IsOwnerOrAdmin());
        }

        [HttpGet]
        [RequirePermission("Pass Slip:View")]
        [ProducesResponseType(typeof(ResponseModel<List<PassSlipApplicationModel>>), 200)]
        public async Task<IActionResult> Get(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] Guid? employeeId,
            CancellationToken token)
        {
            var data = await _service.FindAllAsync(from, to, employeeId, token);
            return Ok(_mapper.Map<List<PassSlipApplicationModel>>(data));
        }

        [HttpGet("{id}")]
        [RequirePermission("Pass Slip:View")]
        [ProducesResponseType(typeof(ResponseModel<PassSlipApplicationModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<PassSlipApplicationModel>(data));
        }

        [HttpPost]
        [RequirePermission("Pass Slip:Create")]
        [ProducesResponseType(typeof(ResponseModel<PassSlipApplicationModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreatePassSlipApplication payload, CancellationToken token)
        {
            var data = _mapper.Map<PassSlipApplication>(payload);
            await _service.AddAsync(data, token);
            return Ok(_mapper.Map<PassSlipApplicationModel>(data));
        }

        // Clean edit -- no approval fields on this payload, Approve/Revoke are dedicated
        // endpoints below, so no any-of needed here unlike Leave/Overtime/Official Business.
        [HttpPut("{id}")]
        [RequirePermission("Pass Slip:Edit")]
        [ProducesResponseType(typeof(ResponseModel<PassSlipApplicationModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdatePassSlipApplication payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<PassSlipApplicationModel>(payload));
        }

        [HttpPost("{id}/approve")]
        [RequirePermission("Pass Slip:Approve")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Approve(Guid id, [FromBody] ApprovalActionRequest? body, CancellationToken token)
        {
            var (approverEmployeeId, hasOverride) = await ResolveApproverAsync(token);
            await _service.ApproveAsync(id, approverEmployeeId, hasOverride, body?.Note, token);
            return Ok();
        }

        // Rejects a still-pending pass slip -- see Revoke below for un-approving one that
        // already went through.
        [HttpPost("{id}/decline")]
        [RequirePermission("Pass Slip:Approve")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Decline(Guid id, [FromBody] ApprovalActionRequest? body, CancellationToken token)
        {
            var (approverEmployeeId, hasOverride) = await ResolveApproverAsync(token);
            await _service.DeclineAsync(id, approverEmployeeId, hasOverride, body?.Note, token);
            return Ok();
        }

        // Un-approves an already-approved pass slip -- reject side of the same decision
        // workflow as Approve, same permission.
        [HttpPost("{id}/revoke")]
        [RequirePermission("Pass Slip:Approve")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Revoke(Guid id, CancellationToken token)
        {
            await _service.RevokeAsync(id, token);
            return Ok();
        }

        [HttpDelete("{id}")]
        [RequirePermission("Pass Slip:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
