using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class TimeShiftsController : ControllerBase
    {
        private readonly TimeShiftService _service;
        private readonly IMapper _mapper;

        public TimeShiftsController(TimeShiftService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }


        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<TimeShiftModel>(data));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateTimeShift payload, CancellationToken token)
        {
            return Ok(await _service.AddAsync(payload, token));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateTimeShift payload, CancellationToken token)
        {
            var data = await _service.UpdateAsync(id, payload, token);
            return Ok(data);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }
    }
}
