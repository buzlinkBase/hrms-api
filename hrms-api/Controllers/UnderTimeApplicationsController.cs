using Asp.Versioning;
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
    public class UnderTimeApplicationsController : ControllerBase
    {
        private readonly UnderTimeApplicationService _service;
        private readonly IMapper _mapper;

        public UnderTimeApplicationsController(UnderTimeApplicationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [RequirePermission("Undertime:View")]
        [ProducesResponseType(typeof(ResponseModel<List<UnderTimeApplicationModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken token)
        {
            var data = await _service.FindAllAsync(token, from, to);
            return Ok(_mapper.Map<List<UnderTimeApplicationModel>>(data));
        }

        [HttpGet("{id}")]
        [RequirePermission("Undertime:View")]
        [ProducesResponseType(typeof(ResponseModel<UnderTimeApplicationModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<UnderTimeApplicationModel>(data));
        }

        [HttpPost]
        [RequirePermission("Undertime:Create")]
        [ProducesResponseType(typeof(ResponseModel<UnderTimeApplicationModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateUnderTimeApplication payload, CancellationToken token)
        {
            var data = _mapper.Map<UnderTimeApplication>(payload);
            await _service.AddAsync(data, token);
            return Ok(_mapper.Map<UnderTimeApplicationModel>(data));
        }

        [HttpPost("batch")]
        [RequirePermission("Undertime:Create")]
        [ProducesResponseType(typeof(ResponseModel<List<UnderTimeApplicationModel>>), 200)]
        public async Task<IActionResult> PostBatch([FromBody] List<CreateUnderTimeApplication> payload, CancellationToken token)
        {
            var results = new List<UnderTimeApplicationModel>();
            foreach (var item in payload)
            {
                var data = _mapper.Map<UnderTimeApplication>(item);
                data.ApprovalStatus = ApprovalStatus.Approved;
                await _service.AddAsync(data, token);
                results.Add(_mapper.Map<UnderTimeApplicationModel>(data));
            }
            return Ok(results);
        }

        [HttpPut("{id}")]
        [RequirePermission("Undertime:Edit", "Undertime:Approve")]
        [ProducesResponseType(typeof(ResponseModel<UnderTimeApplicationModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateUnderTimeApplication payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<UnderTimeApplicationModel>(payload));
        }

        [HttpDelete("{id}")]
        [RequirePermission("Undertime:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
