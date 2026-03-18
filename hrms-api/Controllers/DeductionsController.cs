using Asp.Versioning;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class DeductionsController : ControllerBase
    {
        private readonly DeductionService _service;
        private readonly IMapper _mapper;
        public DeductionsController(DeductionService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<DeductionModel>>(data));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<DeductionModel>(data));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateDeduction payload, CancellationToken token)
        {
            var data = _mapper.Map<Deduction>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<DeductionModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateDeduction payload, CancellationToken token)
        {
            var data = _mapper.Map<Deduction>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<DeductionModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id,token);
            return Ok();
        }
    }
}
