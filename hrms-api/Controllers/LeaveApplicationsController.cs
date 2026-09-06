using Asp.Versioning;
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
        private readonly IMapper _mapper;

        public LeaveApplicationsController(LeaveApplicationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet("range")]
        [ProducesResponseType(typeof(ResponseModel<List<LeaveApplicationModel>>), 200)]
        public async Task<IActionResult> Range([FromQuery] DateEmployeeRequestPayload payload, CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(data);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<LeaveApplicationModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken token)
        {
            var data = await _service.FindAllAsync(token, from, to);
            return Ok(data);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<LeaveApplicationModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(data);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateLeaveApplication payload, CancellationToken token)
        {
            await _service.AddAsync(payload, token);
            await _service.CommitChangesAsync(token);
            return Ok();
        }

        [HttpPost("batch")]
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

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<LeaveApplicationModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateLeaveApplication payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<LeaveApplicationModel>(payload));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            await _service.CommitChangesAsync(token);
            return Ok();
        }

        [HttpPut("{id}/reimbursement")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> UpdateReimbursement(Guid id, [FromBody] UpdateReimbursementStatus payload, CancellationToken token)
        {
            await _service.UpdateReimbursementStatusAsync(id, payload, token);
            return Ok();
        }
    }
}
