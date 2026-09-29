using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Core.Services;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/cash-bond")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    [EntriesData]
    public class CashBondController : ControllerBase
    {
        private readonly PayrollReportService _reportService;

        public CashBondController(PayrollReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>
        /// Manual HR entry against one employee's cash bond collected total -- an additive delta
        /// + direction (IsAddition), not "set a new total". Typically the opening balance of cash
        /// bond deducted before this system was adopted. A removal is clamped so the total never
        /// goes negative. Mirrors RetirementController.Adjust.
        /// </summary>
        [HttpPost("adjust")]
        [RequirePermission("Payroll Reports:Edit")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Adjust([FromBody] AdjustCashBondPayload payload, CancellationToken token)
        {
            await _reportService.AdjustCashBondAsync(
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
