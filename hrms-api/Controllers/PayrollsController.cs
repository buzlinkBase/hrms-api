using System.Security.Claims;
using Asp.Versioning;
using Hrms.Api.Documents;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

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
        // Maps a batch's actual run type to its Payroll Generation catalog row -- needed by
        // PostBatch/DeleteBatch below, which are shared across all 4 run types and so can't be
        // gated with a compile-time RequirePermissionAttribute code the way every other action
        // here is.
        private static readonly Dictionary<PayrollType, string> RunTypeFeature = new()
        {
            [PayrollType.Regular] = "Payroll Run",
            [PayrollType.ThirteenthMonth] = "13th Month Run",
            [PayrollType.LastPay] = "Last Pay Run",
            [PayrollType.YearEndAdjustment] = "Year-End Adjustment Run",
        };

        private readonly PayrollProcessorService _service;
        private readonly PayrollService _payrollService;
        private readonly EmployeeService _employeeService;
        private readonly CompanyService _companyService;
        private readonly PayrollBatchService _payrollBatchService;
        private readonly ClientService _clientService;

        public PayrollsController(PayrollProcessorService service, PayrollService payrollService, EmployeeService employeeService, CompanyService companyService, PayrollBatchService payrollBatchService, ClientService clientService)
        {
            _service = service;
            _payrollService = payrollService;
            _employeeService = employeeService;
            _companyService = companyService;
            _payrollBatchService = payrollBatchService;
            _clientService = clientService;
        }

        // Looks up batchId's actual run type and checks the caller holds "{run type}:{action}"
        // -- mirrors WorkSchedulePlansController.ValidateTeamScopeAsync's shape (internal, takes
        // an explicit ClaimsPrincipal, returns null on success or the IActionResult to
        // short-circuit with) so it's directly unit-testable without the action pipeline.
        internal async Task<IActionResult?> ValidateBatchPermissionAsync(ClaimsPrincipal user, Guid batchId, string action, CancellationToken token)
        {
            var batch = await _payrollBatchService.FineOneAsync(batchId, token);
            if (batch == null) return NotFound();
            if (!user.HasAnyPermission($"{RunTypeFeature[batch.PayrollType]}:{action}")) return Forbid();
            return null;
        }

        [HttpPost("calculate")]
        [RequirePermission("Payroll Run:Create")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Calculate([FromBody] PayrollRunPayload payload, CancellationToken token)
        {
            var batch = Guid.CreateVersion7();
            var payrolls = await _service.CalculateAsync(payload, batch, token);
            var response = new { data = payrolls, total = payrolls.Count };
            return Ok(response);
        }

        [HttpPost("generate")]
        [RequirePermission("Payroll Run:Create")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Generate([FromBody] PayrollRunPayload payload, CancellationToken token)
        {
            var batch = Guid.CreateVersion7();
            var payrolls = await _service.GenerateAsync(payload, batch, token);
            var response = new { data = payrolls, total = payrolls.Count };
            return Ok(response);
        }

        // Lump-sum 13th month pay run — not attendance-driven, so no DTR batch selection.
        // Post/Delete reuse the same batch/{id}/post and batch/{id} endpoints below.
        [HttpPost("generate-13th-month")]
        [RequirePermission("13th Month Run:Create")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> GenerateThirteenthMonth([FromBody] ThirteenthMonthRunPayload payload, CancellationToken token)
        {
            var payrolls = await _service.GenerateThirteenthMonthAsync(payload, token);
            var response = new { data = payrolls, total = payrolls.Count };
            return Ok(response);
        }

        [HttpPost("generate-last-pay")]
        [RequirePermission("Last Pay Run:Create")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> GenerateLastPay([FromBody] LastPayRunPayload payload, CancellationToken token)
        {
            var payrolls = await _service.GenerateLastPayAsync(payload, token);
            var response = new { data = payrolls, total = payrolls.Count };
            return Ok(response);
        }

        // Year-End Tax Annualization review step — recomputes each in-scope employee's true
        // annual tax due vs. tax withheld YTD without persisting anything, so HR can review
        // before Generate is called. MWE-excluded and already-generated employees are returned
        // flagged, not omitted, so it's clear why they show no (or zero) adjustment.
        [HttpPost("preview-year-end-adjustment")]
        [RequirePermission("Year-End Adjustment Run:Create")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> PreviewYearEndAdjustment([FromBody] TaxAnnualizationRunPayload payload, CancellationToken token)
        {
            var preview = await _service.PreviewYearEndAdjustmentAsync(payload, token);
            var response = new { data = preview, total = preview.Count };
            return Ok(response);
        }

        [HttpPost("generate-year-end-adjustment")]
        [RequirePermission("Year-End Adjustment Run:Create")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> GenerateYearEndAdjustment([FromBody] TaxAnnualizationRunPayload payload, CancellationToken token)
        {
            var payrolls = await _service.GenerateYearEndAdjustmentAsync(payload, token);
            var response = new { data = payrolls, total = payrolls.Count };
            return Ok(response);
        }

        // Review-step data for the Last Pay generation screen — every Salary Adjustment /
        // Other Income row not yet consumed by any payroll run for these employees, for HR to
        // check off before Generate is called (LastPayRunPayload.SalaryAdjustmentIds /
        // OtherIncomeScheduleIds). Nothing here is applied just by being listed.
        [HttpGet("last-pay/available-salary-adjustments")]
        [RequirePermission("Last Pay Run:View")]
        [ProducesResponseType(typeof(ResponseModel<List<SalaryAdjustment>>), 200)]
        public async Task<IActionResult> GetAvailableSalaryAdjustments([FromQuery] List<Guid> employeeIds, CancellationToken token)
        {
            var data = await _service.GetAvailableSalaryAdjustmentsAsync(employeeIds, token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("last-pay/available-other-income")]
        [RequirePermission("Last Pay Run:View")]
        [ProducesResponseType(typeof(ResponseModel<List<OtherIncomeSchedules>>), 200)]
        public async Task<IActionResult> GetAvailableOtherIncome([FromQuery] List<Guid> employeeIds, CancellationToken token)
        {
            var data = await _service.GetAvailableOtherIncomeAsync(employeeIds, token);
            return Ok(new { data, total = data.Count });
        }

        // Informational only — flags employees with posted attendance after their last
        // regular payroll that no regular run has ever paid out. Never blocks generation.
        [HttpGet("last-pay/attendance-warnings")]
        [RequirePermission("Last Pay Run:View")]
        [ProducesResponseType(typeof(ResponseModel<List<LastPayAttendanceWarning>>), 200)]
        public async Task<IActionResult> GetLastPayAttendanceWarnings([FromQuery] List<Guid> employeeIds, CancellationToken token)
        {
            var data = await _service.GetLastPayAttendanceWarningsAsync(employeeIds, token);
            return Ok(new { data, total = data.Count });
        }

        // Informational only — each selected separated employee's Cash Bond collected-to-date
        // vs. target, for HR to review. Never applied to NetPay automatically; see
        // LastPayrollService.GetCashBondStatusAsync.
        [HttpGet("last-pay/cash-bond-status")]
        [RequirePermission("Last Pay Run:View")]
        [ProducesResponseType(typeof(ResponseModel<List<CashBondReportModel>>), 200)]
        public async Task<IActionResult> GetLastPayCashBondStatus([FromQuery] List<Guid> employeeIds, CancellationToken token)
        {
            var data = await _service.GetCashBondStatusAsync(employeeIds, token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet]
        [RequirePermission("Payroll Summary:View")]
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

        [HttpGet("print-summary")]
        [RequirePermission("Payroll Summary:Export")]
        public async Task<IActionResult> PrintSummary(
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
            var company = await _companyService.FineOneAsync(token);
            var clientIds = data.Where(x => x.ClientId.HasValue).Select(x => x.ClientId!.Value).ToHashSet();
            var clientNames = await _clientService.FindNamesByIdsAsync(clientIds, token);
            var document = new PayrollSummaryReportDocument(data, fromDate, toDate, company, clientNames);
            var bytes = document.GeneratePdf();
            return File(bytes, "application/pdf", $"payroll-summary-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf");
        }

        [HttpGet("{id:guid}/print")]
        [RequirePermission("Payroll Summary:Export")]
        public async Task<IActionResult> PrintPayslip(Guid id, CancellationToken token)
        {
            var payroll = await _payrollService.FineOneAsync(id, token);
            if (payroll == null) return NotFound();
            var employee = await _employeeService.GetFullByIdAsync(payroll.EmployeeId, token);
            if (employee == null) return NotFound();
            var company = await _companyService.FineOneAsync(token);
            // Format follows the OT/ND calculation method actually used for this payroll run
            // (recorded per-row on Payroll.OtNdCalculationMethod, not the live company setting,
            // so an old payslip always reprints the format it was originally correct for) --
            // Additive mode's Hours-breakdown format is the one whose itemized rows reconcile to
            // Gross Income exactly; Compounded's Standard format is the client-approved default.
            // No longer user-selectable -- see PayslipHoursDocument's own doc comment.
            IDocument document = payroll.OtNdCalculationMethod == OtNdCalculationMethod.Additive
                ? new PayslipHoursDocument(payroll, employee, company)
                : new PayslipDocument(payroll, employee, company);
            var bytes = document.GeneratePdf();
            return File(bytes, "application/pdf", $"payslip-{employee.EmployeeNo}-{payroll.PayPeriodStart:yyyyMMdd}.pdf");
        }

        // Post and Delete are run-level transactions — an employee's payroll is never
        // generated on its own, so it's never posted or deleted on its own either. Both act
        // against the PayrollBatch header row (id = Payroll.PayrollBatchId). See
        // PayrollProcessorService.PostBatchAsync / DeleteBatchAsync.
        [HttpPost("batch/{batchId:guid}/post")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> PostBatch(Guid batchId, CancellationToken token)
        {
            var violation = await ValidateBatchPermissionAsync(User, batchId, "Approve", token);
            if (violation != null) return violation;

            await _service.PostBatchAsync(batchId, token);
            return Ok("success");
        }

        [HttpDelete("batch/{batchId:guid}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> DeleteBatch(Guid batchId, CancellationToken token)
        {
            var violation = await ValidateBatchPermissionAsync(User, batchId, "Create", token);
            // Deleting a batch that's already gone (a second click, a stale list, a concurrent
            // delete) should succeed silently -- the caller's desired end state already holds,
            // so this isn't an error. ValidateBatchPermissionAsync can't permission-check a
            // batch it can't find, but there's nothing left to protect either.
            if (violation is NotFoundResult or NotFoundObjectResult) return Ok("success");
            if (violation != null) return violation;

            await _service.DeleteBatchAsync(batchId, token);
            return Ok("success");
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
