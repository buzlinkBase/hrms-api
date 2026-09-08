using Asp.Versioning;
using Hrms.Api.Documents;
using Hrms.Api.Extensions;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;

namespace Hrms.Api.Controllers
{
    // Self-service ("My Portal") endpoints. Unlike the admin controllers (EmployeesController,
    // PayrollsController), every action here resolves the caller's own EmployeeId server-side
    // from the JWT (sub/email claims) and never accepts a client-supplied employeeId — that's
    // what makes it safe to expose to any authenticated user, not just HR staff.
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class MeController : ControllerBase
    {
        private readonly EmployeeService _employeeService;
        private readonly PayrollService _payrollService;
        private readonly CompanyService _companyService;
        private readonly DTRCalcService _dtrCalcService;
        private readonly EmployeeFixedScheduleService _fixedScheduleService;

        public MeController(
            EmployeeService employeeService,
            PayrollService payrollService,
            CompanyService companyService,
            DTRCalcService dtrCalcService,
            EmployeeFixedScheduleService fixedScheduleService)
        {
            _employeeService = employeeService;
            _payrollService = payrollService;
            _companyService = companyService;
            _dtrCalcService = dtrCalcService;
            _fixedScheduleService = fixedScheduleService;
        }

        private async Task<Guid?> ResolveMyEmployeeIdAsync(CancellationToken token)
        {
            var userId = User.GetRequiredUserId();
            var email = User.GetUserClaim("email");
            return await _employeeService.ResolveEmployeeIdAsync(userId, email, token);
        }

        [HttpGet("employee")]
        public async Task<IActionResult> GetMyEmployee(CancellationToken token)
        {
            var userId = User.GetRequiredUserId();
            var email = User.GetUserClaim("email");
            var employee = await _employeeService.GetFullByUserOrEmailAsync(userId, email, token);
            if (employee == null) return NotFound();
            return Ok(employee);
        }

        [HttpGet("payrolls")]
        [ProducesResponseType(typeof(ResponseModel<List<Payroll>>), 200)]
        public async Task<IActionResult> GetMyPayrolls([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var data = await _payrollService.GetAsync(fromDate, toDate, employeeId, null, null, token);
            return Ok(new { data, total = data.Count });
        }

        [HttpGet("payrolls/{id:guid}/print")]
        public async Task<IActionResult> PrintMyPayslip(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var payroll = await _payrollService.FineOneAsync(id, token);
            if (payroll == null || payroll.EmployeeId != employeeId) return NotFound();

            var employee = await _employeeService.GetFullByIdAsync(payroll.EmployeeId, token);
            if (employee == null) return NotFound();
            var company = await _companyService.FineOneAsync(token);
            var document = new PayslipDocument(payroll, employee, company);
            var bytes = document.GeneratePdf();
            return File(bytes, "application/pdf", $"payslip-{employee.EmployeeNo}-{payroll.PayPeriodStart:yyyyMMdd}.pdf");
        }

        [HttpGet("dtr-detail")]
        [ProducesResponseType(typeof(ResponseModel<ObjectCollection<DTRDetailModel>>), 200)]
        public async Task<IActionResult> GetMyDtrDetail([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var payload = new DTRRequestPayload { FromDate = DateOnly.FromDateTime(from), ToDate = DateOnly.FromDateTime(to), EmployeeId = employeeId };
            var result = await _dtrCalcService.GetDTRInfoAsync<DTRDetailModel>(payload, ProcessorType.DTRDetail, token, IncludeNullResponse.Include);
            return Ok(result);
        }

        [HttpGet("incomplete-punches")]
        [ProducesResponseType(typeof(ResponseModel<ObjectCollection<ColumnarLogModel>>), 200)]
        public async Task<IActionResult> GetMyIncompletePunches([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var payload = new DTRRequestPayload { FromDate = DateOnly.FromDateTime(from), ToDate = DateOnly.FromDateTime(to), EmployeeId = employeeId };
            var result = await _dtrCalcService.GetDTRInfoAsync<ColumnarLogModel>(payload, ProcessorType.CleanColumnarLog, token, IncludeNullResponse.Include, false);
            return Ok(result);
        }

        [HttpGet("fixed-schedule")]
        [ProducesResponseType(typeof(ResponseModel<List<EmployeeFixedScheduleModel>>), 200)]
        public async Task<IActionResult> GetMyFixedSchedule(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            return Ok(await _fixedScheduleService.GetByEmployeeAsync(employeeId.Value, token));
        }
    }
}
