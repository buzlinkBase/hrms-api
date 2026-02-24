using Asp.Versioning;
using AutoMapper;
using Elastic.Clients.Elasticsearch.Core.TermVectors;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class PositionsController : ControllerBase
    {
        private readonly PositionService _service;
        private readonly IMapper _mapper;

        public PositionsController(PositionService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }


        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<PositionModel>>(data));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<PositionModel>(data));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreatePosition payload, CancellationToken token)
        {
            var data = _mapper.Map<Position>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<PositionModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdatePosition payload, CancellationToken token)
        {
            var data = _mapper.Map<Position>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<PositionModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
