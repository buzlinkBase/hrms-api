using Asp.Versioning;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
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
        public async Task<IActionResult> Get([FromQuery] DateOnly effectivity, CancellationToken token)
        {
            var data = await _service.FindAllAsync(effectivity, token);
            return Ok(_mapper.Map<List<SSSModel>>(data));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<SSSModel>(data));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateSSS payload, CancellationToken token)
        {
            var data = _mapper.Map<SSSTable>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<SSSModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateSSS payload, CancellationToken token)
        {
            var data = _mapper.Map<SSSTable>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<SSSModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
