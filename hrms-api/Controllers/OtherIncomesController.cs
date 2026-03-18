using Asp.Versioning;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class OtherIncomesController : ControllerBase
    {
        private readonly OtherIncomeService _service;
        private readonly IMapper _mapper;
        public OtherIncomesController(OtherIncomeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var data = await _service.FindAllAsync();
            var result = _mapper.Map<List<OtherIncomeModel>>(data);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            var result = _mapper.Map<OtherIncomeModel>(data);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateOtherIncome payload, CancellationToken token)
        {
            var data = _mapper.Map<OtherIncome>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<OtherIncomeModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateOtherIncome payload, CancellationToken token)
        {
            var data = _mapper.Map<OtherIncome>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<OtherIncomeModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }
    }
}
