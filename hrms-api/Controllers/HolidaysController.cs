using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class HolidaysController : ControllerBase
    {
        private readonly HolidayService _service;
        private readonly IMapper _mapper;

        public HolidaysController(HolidayService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Get([FromQuery(Name = "year")] int year, CancellationToken token)
        {
            var data = await _service.FindAllAsync(year, token);
            return Ok(data);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<HolidayModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<HolidayModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateHoliday payload, CancellationToken token)
        {
            return Ok(await _service.AddAsync(payload, token));
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateHoliday payload, CancellationToken token)
        {
            payload.Id = id;
            return Ok(await _service.UpdateAsync(payload, token));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }

        [HttpDelete("all")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> DeleteAll([FromQuery] int year, CancellationToken token)
        {
            await _service.DeleteAllAsync(year, token);
            return Ok();
        }
    }
}
