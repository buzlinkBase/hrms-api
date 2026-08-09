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
    public class SSSController : ControllerBase
    {
        private readonly SSSService _service;
        private readonly IMapper _mapper;

        public SSSController(SSSService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<SSSModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] DateOnly effectivity, CancellationToken token)
        {
            var data = await _service.FindAllAsync(effectivity, token);
            return Ok(_mapper.Map<List<SSSModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<SSSModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<SSSModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<SSSModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateSSS payload, CancellationToken token)
        {
            var data = _mapper.Map<SSSTable>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<SSSModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<SSSModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateSSS payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<SSSModel>(payload));
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
