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
    public class HDMFsController : ControllerBase
    {
        private readonly HDMFService _service;
        private readonly IMapper _mapper;

        public HDMFsController(HDMFService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<HDMFModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] DateOnly effectivity, CancellationToken token)
        {
            var data = await _service.FindAllAsync(effectivity, token);
            return Ok(_mapper.Map<List<HDMFModel>>(data));
        }

        [HttpGet("versions")]
        [ProducesResponseType(typeof(ResponseModel<List<DateOnly>>), 200)]
        public async Task<IActionResult> GetVersions(CancellationToken token)
        {
            return Ok(await _service.VersionsAsync(token));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ResponseModel<HDMFModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<HDMFModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<HDMFModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateHDMF payload, CancellationToken token)
        {
            var data = _mapper.Map<HDMFTable>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<HDMFModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<HDMFModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateHDMF payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<HDMFModel>(payload));
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
