using Asp.Versioning;
using Hrms.Domain.Entities;
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
        private readonly PayrollService _payrollService;

        public PayrollsController(PayrollProcessorService service, PayrollService payrollService)
        {
            _service = service;
            _payrollService = payrollService;
        }

        [HttpPost("calculate")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Calculate([FromBody] PayrollRunPayload payload, CancellationToken token)
        {
            var payrolls = await _service.CalculateAsync(payload, token);
            var response = new { data = payrolls, total = payrolls.Count };
            return Ok(response);
        }

        [HttpPost("generate")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Generate([FromBody] PayrollRunPayload payload, CancellationToken token)
        {
            var payrolls = await _service.GenerateAsync(payload, token);
            var response = new { data = payrolls, total = payrolls.Count };
            return Ok(response);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<Payroll>>), 200)]
        public async Task<IActionResult> Get(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] Guid? employeeId,
            [FromQuery] Guid? clientId,
            [FromQuery] Guid? payrollGroupId,
            CancellationToken token)
        {
            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var data = await _payrollService.GetAsync(fromDate, toDate, employeeId, clientId, payrollGroupId, token);
            return Ok(new { data, total = data.Count });
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
