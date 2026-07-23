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
    public class ChangeHolidaysController : ControllerBase
    {
        private readonly ChangeHolidayService _service;
        private readonly IMapper _mapper;
        public ChangeHolidaysController(ChangeHolidayService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<string>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateChangeHoliday payload, CancellationToken token)
        {
            await _service.AddAsync(payload, token);
            return Ok("success");
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<ChangeHolidayModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] ChangeHolidayQueryPayload payload, CancellationToken token)
        {
            var data = await _service.LoadAllAsync(payload, token);
            return Ok(data);
        }

        [HttpDelete]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> DeleteEmp([FromQuery] Guid employeeId, [FromQuery] string BatchCode, CancellationToken token)
        {
            await _service.DeleteEmployee(employeeId, BatchCode, token);
            return Ok();
        }

        [HttpDelete("batch")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> DeleteBatch([FromQuery] string BatchCode, CancellationToken token)
        {
            await _service.DeleteAsync(BatchCode, token);
            return Ok();
        }

    }
}
