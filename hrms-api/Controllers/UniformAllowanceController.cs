using Asp.Versioning;
using Hrms.Api.Filters;
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
    public class UniformAllowanceController : ControllerBase
    {
        private readonly UniformAllowanceFundService _service;

        public UniformAllowanceController(UniformAllowanceFundService service)
        {
            _service = service;
        }

        /// <summary>
        /// Current UniformAllowanceFund.Balance per employee — used by the Release review step
        /// to default each selected employee's amount to their true current balance (not a
        /// date-range-dependent value read off the ledger report itself).
        /// </summary>
        [HttpGet("balances")]
        [RequirePermission("Payroll Reports:View")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Balances([FromQuery] List<Guid> employeeIds, CancellationToken token)
        {
            var balances = await _service.GetBalancesAsync(employeeIds, token);
            return Ok(balances.Select(kv => new { employeeId = kv.Key, balance = kv.Value }));
        }

        /// <summary>
        /// Manual HR correction against one employee's Uniform Allowance balance — an additive
        /// delta + direction (IsAddition), not "set a new balance". Writes one Adjustment ledger
        /// entry; a Remove is clamped so it can never drive the balance negative.
        /// </summary>
        [HttpPost("adjust")]
        [RequirePermission("Payroll Reports:Edit")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Adjust([FromBody] AdjustUniformAllowancePayload payload, CancellationToken token)
        {
            await _service.AdjustAsync(
                payload.EmployeeId,
                payload.Amount,
                payload.IsAddition,
                payload.Particulars,
                payload.EntryDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                token);
            return Ok();
        }

        /// <summary>
        /// HR disburses accumulated Uniform Allowance for a specific period, for one or more
        /// employees at once. Each employee's requested amount is independently clamped to their
        /// own current balance — clampedEmployeeIds names whoever got clamped so the caller can
        /// flag it rather than silently releasing less than what was requested.
        /// </summary>
        [HttpPost("release")]
        [RequirePermission("Payroll Reports:Edit")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Release([FromBody] ReleaseUniformAllowancePayload payload, CancellationToken token)
        {
            var clamped = await _service.ReleaseBatchAsync(
                payload.Releases.Select(r => (r.EmployeeId, r.Amount)).ToList(),
                payload.PeriodDate,
                payload.Particulars,
                token);
            return Ok(new { clampedEmployeeIds = clamped });
        }
    }
}
