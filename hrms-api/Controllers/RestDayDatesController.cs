using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]

    public class RestDayDatesController : ControllerBase
    {
        private readonly RestDayDateService _service;
        public RestDayDatesController(RestDayDateService service)
        {
            _service = service;
        }
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateRestDayDate payload, CancellationToken token)
        {
            var data = await _service.AddOrUpdate(payload, token);
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] Guid employee_id, CancellationToken token)
        {
            var data = await _service.FindAllAsync(employee_id, token);
            return Ok(data);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FindOneAsync(id, token);
            return Ok(data);
        }
       

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Remove(id, token);
            return Ok("success");
        }
    }
}
