using Asp.Versioning;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class EmployeeFixedSchedulesController : ControllerBase
    {
        private readonly EmployeeFixedScheduleService _service;

        public EmployeeFixedSchedulesController(EmployeeFixedScheduleService service)
        {
            _service = service;
        }

        [HttpGet("employee/{employeeId:guid}")]
        [ProducesResponseType(typeof(ResponseModel<List<EmployeeFixedScheduleModel>>), 200)]
        public async Task<IActionResult> GetByEmployee(Guid employeeId, CancellationToken token)
        {
            return Ok(await _service.GetByEmployeeAsync(employeeId, token));
        }

        [HttpPut("employee/{employeeId:guid}/day/{dayName}")]
        [ProducesResponseType(typeof(ResponseModel<EmployeeFixedScheduleModel>), 200)]
        public async Task<IActionResult> SetDay(Guid employeeId, DayName dayName, [FromBody] SetEmployeeFixedScheduleDay payload, CancellationToken token)
        {
            return Ok(await _service.SetDayAsync(employeeId, dayName, payload.TimeShiftId, token));
        }

        [HttpDelete("employee/{employeeId:guid}/day/{dayName}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> UnassignDay(Guid employeeId, DayName dayName, CancellationToken token)
        {
            await _service.UnassignDayAsync(employeeId, dayName, token);
            return Ok();
        }
    }
}
