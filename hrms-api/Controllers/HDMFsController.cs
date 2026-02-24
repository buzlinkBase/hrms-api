using Asp.Versioning;
using AutoMapper;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
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
        public async Task<IActionResult> Get([FromQuery] DateOnly effectivity, CancellationToken token)
        {
            var data = await _service.FindAllAsync(effectivity, token);
            return Ok(_mapper.Map<List<HDMFModel>>(data));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<HDMFModel>(data));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateHDMF payload, CancellationToken token)
        {
            var data = _mapper.Map<HDMFTable>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<HDMFModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateHDMF payload, CancellationToken token)
        {
            var data = _mapper.Map<HDMFTable>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<HDMFModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
