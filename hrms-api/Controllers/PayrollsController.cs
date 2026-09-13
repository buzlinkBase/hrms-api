using Asp.Versioning;
using Hrms.Api.Documents;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;

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
        private readonly EmployeeService _employeeService;
        private readonly CompanyService _companyService;

        public PayrollsController(PayrollProcessorService service, PayrollService payrollService, EmployeeService employeeService, CompanyService companyService)
        {
            _service = service;
            _payrollService = payrollService;
            _employeeService = employeeService;
            _companyService = companyService;
        }

        [HttpPost("calculate")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Calculate([FromBody] PayrollRunPayload payload, CancellationToken token)
        {
            var batch = Guid.CreateVersion7();
            var payrolls = await _service.CalculateAsync(payload, batch, token);
            var response = new { data = payrolls, total = payrolls.Count };
            return Ok(response);
        }

        [HttpPost("generate")]
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
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> GenerateThirteenthMonth([FromBody] ThirteenthMonthRunPayload payload, CancellationToken token)
        {
            var payrolls = await _service.GenerateThirteenthMonthAsync(payload, token);
            var response = new { data = payrolls, total = payrolls.Count };
            return Ok(response);
        }

        [HttpPost("generate-last-pay")]
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
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> PreviewYearEndAdjustment([FromBody] TaxAnnualizationRunPayload payload, CancellationToken token)
        {
            var preview = await _service.PreviewYearEndAdjustmentAsync(payload, token);
            var response = new { data = preview, total = preview.Count };
            return Ok(response);
        }

        [HttpPost("generate-year-end-adjustment")]
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
        [ProducesResponseType(typeof(ResponseModel<List<SalaryAdjustment>>), 200)]
        public async Task<IActionResult> GetAvailableSalaryAdjustments([FromQuery] List<Guid> employeeIds, CancellationToken token)
        {
            var data = await _service.GetAvailableSalaryAdjustmentsAsync(employeeIds, token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("last-pay/available-other-income")]
        [ProducesResponseType(typeof(ResponseModel<List<OtherIncomeSchedules>>), 200)]
        public async Task<IActionResult> GetAvailableOtherIncome([FromQuery] List<Guid> employeeIds, CancellationToken token)
        {
            var data = await _service.GetAvailableOtherIncomeAsync(employeeIds, token);
            return Ok(new { data, total = data.Count });
        }

        // Informational only — flags employees with posted attendance after their last
        // regular payroll that no regular run has ever paid out. Never blocks generation.
        [HttpGet("last-pay/attendance-warnings")]
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
        [ProducesResponseType(typeof(ResponseModel<List<CashBondReportModel>>), 200)]
        public async Task<IActionResult> GetLastPayCashBondStatus([FromQuery] List<Guid> employeeIds, CancellationToken token)
        {
            var data = await _service.GetCashBondStatusAsync(employeeIds, token);
            return Ok(new { data, total = data.Count });
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

        [HttpGet("print-summary")]
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
            var document = new PayrollSummaryReportDocument(data, fromDate, toDate, company);
            var bytes = document.GeneratePdf();
            return File(bytes, "application/pdf", $"payroll-summary-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.pdf");
        }

        [HttpGet("{id:guid}/print")]
        public async Task<IActionResult> PrintPayslip(Guid id, CancellationToken token)
        {
            var payroll = await _payrollService.FineOneAsync(id, token);
            if (payroll == null) return NotFound();
            var employee = await _employeeService.GetFullByIdAsync(payroll.EmployeeId, token);
            if (employee == null) return NotFound();
            var company = await _companyService.FineOneAsync(token);
            var document = new PayslipDocument(payroll, employee, company);
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
            await _service.PostBatchAsync(batchId, token);
            return Ok("success");
        }

        [HttpDelete("batch/{batchId:guid}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> DeleteBatch(Guid batchId, CancellationToken token)
        {
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
