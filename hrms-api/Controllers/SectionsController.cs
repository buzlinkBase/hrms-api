using Asp.Versioning;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class SectionsController : ControllerBase
    {
        private readonly SectionService _service;
        private readonly IMapper _mapper;

        public SectionsController(SectionService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<SectionModel>>(data));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<SectionModel>(data));
        }

        [HttpGet("department/{id}")]
        public async Task<IActionResult> GetByDept(Guid id, CancellationToken token)
        {
            var data = await _service.FindByDepartmentsAsync(id, token);
            return Ok(_mapper.Map<SectionModel[]>(data));
        }


        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateSection payload, CancellationToken token)
        {
            var data = _mapper.Map<Section>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<SectionModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateSection payload, CancellationToken token)
        {
            var data = _mapper.Map<Section>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<SectionModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
