using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class ChangeHolidaysController : ControllerBase
    {
        private readonly ChangeHolidayService _service;
        private readonly IMapper _mapper;
        public ChangeHolidaysController(ChangeHolidayService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] ChangeHolidayQueryPayload payload, CancellationToken token)
        {
            var data = await _service.LoadAllAsync(payload, token);
            return Ok(data);
        }

        //[HttpGet("{id}")]
        //public async Task<IActionResult> Get(Guid id)
        //{
        //    var data = await _service.FineOneAsync(id);
        //    return Ok(_mapper.Map<HolidaysModel>(data));
        //}

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateChangeHoliday payload, CancellationToken token)
        {
            await _service.AddAsync(payload, token);
            return Ok("success");
        }

        //[HttpPut("{id}")]
        //public async Task<IActionResult> Put(Guid id, [FromBody] UpdateHoliday payload)
        //{
        //    var data = _mapper.Map<Holiday>(payload);
        //    data.Id = id;
        //    await _service.UpdateAsync(data);
        //    await _service.CommitChangesAsync();
        //    return Ok(_mapper.Map<HolidaysModel>(data));
        //}

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }
    }
}
