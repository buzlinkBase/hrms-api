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
    public class PayrollsController : ControllerBase
    {
        private readonly PayrollProcessorService _service;
        public PayrollsController(PayrollProcessorService service)
        {
            _service = service;
        }

        [HttpPost("calculate")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Calculate([FromBody] PayrollCalcPayload payload, CancellationToken token)
        {
            var payrolls = await _service.CalculateAsync(payload, token);
            var response = new
            {
                data = payrolls,
                total = payrolls.Count(),
            };
            return Ok(response);
        }

        //[HttpPost("create-payroll")]
        //public async Task<IActionResult> Calculate([FromBody] PayrollCalcPayload payload)
        //{
        //    var payrolls = await _service.Calculate(payload);
        //    var response = new
        //    {
        //        data = payrolls,
        //        total = payrolls.Count(),
        //    };
        //    return Ok(response);
        //}
    }
}
