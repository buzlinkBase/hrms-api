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
    public class ChangeRestDaysController : ControllerBase
    {
        private readonly ChangeRestDayService _service;
        private readonly IMapper _mapper;
        public ChangeRestDaysController(ChangeRestDayService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Post([FromBody] ChangeOffModel payload, CancellationToken token)
        {
            await _service.AddChangeOff(payload, token);
            return Ok();
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<RestDayRecordResponse>>), 200)]
        public async Task<IActionResult> Get([FromQuery] RestDayListFilter payload, CancellationToken token)
        {
            var data = await _service.FindList(payload, token);
            return Ok(data);
        }

        [HttpDelete()]
        [ProducesResponseType(typeof(ResponseModel<string>), 200)]
        public async Task<IActionResult> Delete([FromQuery] Guid employeeId, [FromQuery] string batchCode, CancellationToken token)
        {
            await _service.DeleteEmployee(employeeId, batchCode, token);
            return Ok("success");
        }
        [HttpDelete("batch")]
        [ProducesResponseType(typeof(ResponseModel<string>), 200)]
        public async Task<IActionResult> DeleteBatch([FromQuery] Guid employeeId, [FromQuery] string batchCode, CancellationToken token)
        {
            await _service.DeleteBatch(batchCode, token);
            return Ok("success");
        }

        [HttpPut("approve")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Approve([FromQuery] Guid employeeId, [FromQuery] string batchCode, CancellationToken token)
        {
            await _service.ApproveChangeOffAsync(batchCode, employeeId, token);
            return Ok();
        }

        [HttpPut("decline")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Decline([FromQuery] Guid employeeId, [FromQuery] string batchCode, CancellationToken token)
        {
            await _service.DeclineChangeOffAsync(batchCode, employeeId, token);
            return Ok();
        }
    }
}
