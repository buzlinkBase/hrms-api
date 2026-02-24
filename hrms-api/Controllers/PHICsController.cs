using Asp.Versioning;
using AutoMapper;
using Elastic.Clients.Elasticsearch.Core.TermVectors;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class PHICsController : ControllerBase
    {
        private readonly PHICService _service;
        private readonly IMapper _mapper;

        public PHICsController(PHICService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }


        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] DateOnly effectivity, CancellationToken token)
        {
            var data = await _service.FindAllAsync(effectivity, token);
            return Ok(_mapper.Map<List<PHICModel>>(data));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<PHICModel>(data));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreatePHIC payload, CancellationToken token)
        {
            var data = _mapper.Map<PHICTable>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<PHICModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdatePHIC payload, CancellationToken token)
        {
            var data = _mapper.Map<PHICTable>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<PHICModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
