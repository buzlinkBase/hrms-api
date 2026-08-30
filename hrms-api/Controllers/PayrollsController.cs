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
