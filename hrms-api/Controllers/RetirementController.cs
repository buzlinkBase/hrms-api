using Asp.Versioning;
using Hrms.Core.Services;
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
    public class RetirementController : ControllerBase
    {
        private readonly PayrollService _payrollService;

        public RetirementController(PayrollService payrollService)
        {
            _payrollService = payrollService;
        }

        /// <summary>
        /// Manual HR correction against one employee's Retirement Fund balance — an additive
        /// delta + direction (IsAddition), not "set a new balance". Useful for seeding an
        /// opening balance when this system is adopted mid-year. Writes one Adjustment ledger
        /// entry; a Remove is clamped so it can never drive the balance negative.
        /// </summary>
        [HttpPost("adjust")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Adjust([FromBody] AdjustRetirementPayload payload, CancellationToken token)
        {
            await _payrollService.AdjustRetirementBalanceAsync(
                payload.EmployeeId,
                payload.Amount,
                payload.IsAddition,
                payload.Particulars,
                payload.EntryDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                token);
            return Ok();
        }
    }
}
