using Onepunch.Common.Lib.Subscriptions;
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
    [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
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

        /// <summary>
        /// HR's quick edit of an employee's per-run cash bond. Applies from the next payroll run and
        /// is logged in the employee's rate history (and the audit trail); the collected total is
        /// not touched. Returns the history row, or null when the rate did not change.
        /// </summary>
        [HttpPut("rate")]
        [RequirePermission("Payroll Reports:Edit")]
        [ProducesResponseType(typeof(ResponseModel<CashBondRateChangeModel>), 200)]
        public async Task<IActionResult> ChangeRate([FromBody] ChangeCashBondRatePayload payload, CancellationToken token) =>
            Ok(await _reportService.ChangeCashBondRateAsync(payload.EmployeeId, payload.NewRate, payload.Reason, token));

        [HttpGet("{employeeId:guid}/rate-history")]
        [RequirePermission("Payroll Reports:View")]
        [ProducesResponseType(typeof(ResponseModel<List<CashBondRateChangeModel>>), 200)]
        public async Task<IActionResult> RateHistory(Guid employeeId, CancellationToken token) =>
            Ok(await _reportService.GetCashBondRateHistoryAsync(employeeId, token));
    }
}
