using Asp.Versioning;
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
        private readonly IMapper _mapper;

        public OvertimeApplicationsController(OvertimeApplicationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper; 
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<OvertimeApplicationModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken token)
        {
            var data = await _service.FindAllAsync(token, from, to);
            return Ok(_mapper.Map<List<OvertimeApplicationModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<OvertimeApplicationModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<OvertimeApplicationModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<OvertimeApplicationModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateOverTimeApplication payload, CancellationToken token)
        {
            var data = _mapper.Map<OverTimeApplication>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<OvertimeApplicationModel>(data);
            return Ok(respModel);
        }

        [HttpPost("batch")]
        [ProducesResponseType(typeof(ResponseModel<List<OvertimeApplicationModel>>), 200)]
        public async Task<IActionResult> PostBatch([FromBody] List<CreateOverTimeApplication> payload, CancellationToken token)
        {
            var entities = _mapper.Map<List<OverTimeApplication>>(payload);
            foreach (var entity in entities)
            {
                entity.ApprovalStatus = ApprovalStatus.Approved;
                await _service.AddAsync(entity, token);
            }
            return Ok(_mapper.Map<List<OvertimeApplicationModel>>(entities));
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<OvertimeApplicationModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateOvertimeApplication payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<OvertimeApplicationModel>(payload));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
