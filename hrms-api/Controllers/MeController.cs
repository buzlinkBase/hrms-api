using Asp.Versioning;
using Hrms.Api.Documents;
using Hrms.Api.Extensions;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MapsterMapper;
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
        private readonly LeaveLedgerService _leaveLedgerService;
        private readonly LeaveApplicationService _leaveApplicationService;
        private readonly OvertimeApplicationService _overtimeApplicationService;
        private readonly TravelOrderApplicationService _travelOrderApplicationService;
        private readonly PassSlipApplicationService _passSlipApplicationService;
        private readonly EmployeeProfileUpdateRequestService _employeeProfileUpdateRequestService;
        private readonly ChangeRestDayService _changeRestDayService;
        private readonly DeductionApplicationService _deductionApplicationService;
        private readonly PayrollReportService _payrollReportService;
        private readonly IMapper _mapper;

        public MeController(
            EmployeeService employeeService,
            PayrollService payrollService,
            CompanyService companyService,
            DTRCalcService dtrCalcService,
            EmployeeFixedScheduleService fixedScheduleService,
            LeaveLedgerService leaveLedgerService,
            LeaveApplicationService leaveApplicationService,
            OvertimeApplicationService overtimeApplicationService,
            TravelOrderApplicationService travelOrderApplicationService,
            PassSlipApplicationService passSlipApplicationService,
            EmployeeProfileUpdateRequestService employeeProfileUpdateRequestService,
            ChangeRestDayService changeRestDayService,
            DeductionApplicationService deductionApplicationService,
            PayrollReportService payrollReportService,
            IMapper mapper)
        {
            _employeeService = employeeService;
            _payrollService = payrollService;
            _companyService = companyService;
            _dtrCalcService = dtrCalcService;
            _fixedScheduleService = fixedScheduleService;
            _leaveLedgerService = leaveLedgerService;
            _leaveApplicationService = leaveApplicationService;
            _overtimeApplicationService = overtimeApplicationService;
            _travelOrderApplicationService = travelOrderApplicationService;
            _passSlipApplicationService = passSlipApplicationService;
            _employeeProfileUpdateRequestService = employeeProfileUpdateRequestService;
            _changeRestDayService = changeRestDayService;
            _deductionApplicationService = deductionApplicationService;
            _payrollReportService = payrollReportService;
            _mapper = mapper;
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

        // Setup > Payslip/13th Month/Last Pay > Received by Employee — informational only,
        // records that the employee has received/viewed this document; nothing else in the
        // system reads or gates on it. Covers all three document types uniformly since
        // Payslip/13th Month/Last Pay are all just Payroll rows differentiated by PayrollType.
        [HttpPost("payrolls/{id:guid}/acknowledge")]
        [ProducesResponseType(typeof(ResponseModel<DateTime?>), 200)]
        public async Task<IActionResult> AcknowledgeMyPayslip(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var acknowledgedAt = await _payrollService.AcknowledgeAsync(id, employeeId.Value, token);
            if (acknowledgedAt == null) return NotFound();
            return Ok(acknowledgedAt);
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

        [HttpGet("leave-credits")]
        [ProducesResponseType(typeof(ResponseModel<List<LeaveCreditsBalanceModel>>), 200)]
        public async Task<IActionResult> GetMyLeaveCredits([FromQuery] int? year, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            return Ok(await _leaveLedgerService.GetBalancesForEmployeeAsync(employeeId.Value, year ?? DateTime.UtcNow.Year, token));
        }

        [HttpGet("leave-applications")]
        [ProducesResponseType(typeof(ResponseModel<List<LeaveApplicationModel>>), 200)]
        public async Task<IActionResult> GetMyLeaveApplications(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            return Ok(await _leaveApplicationService.FindAllForEmployeeAsync(employeeId.Value, token));
        }

        // EmployeeId and ApprovalStatus are forced here, never trusted from the client — the
        // same guard rail as every other write in this controller. Without it, a self-service
        // caller could file leave under a coworker's EmployeeId or self-approve it outright.
        [HttpPost("leave-applications")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> CreateMyLeaveApplication([FromBody] CreateLeaveApplication payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.EmployeeId = employeeId.Value;
            payload.ApprovalStatus = ApprovalStatus.ForApproval;
            await _leaveApplicationService.AddAsync(payload, token, isSelfService: true);
            await _leaveApplicationService.CommitChangesAsync(token);
            return Ok();
        }

        // Ownership + pending-only guard lives in LeaveApplicationService.WithdrawAsync -- it
        // throws NotFoundException (also for "belongs to someone else", so a caller can't probe
        // whether another employee's application id exists) or InvalidOperationException (not
        // ForApproval anymore), both handled by the standard exception pipeline.
        [HttpPatch("leave-applications/{id:guid}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> WithdrawMyLeaveApplication(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _leaveApplicationService.WithdrawAsync(id, employeeId.Value, token);
            return Ok();
        }

        [HttpGet("overtime-applications")]
        [ProducesResponseType(typeof(ResponseModel<List<OverTimeApplication>>), 200)]
        public async Task<IActionResult> GetMyOvertimeApplications(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            return Ok(await _overtimeApplicationService.FindAllForEmployeeAsync(employeeId.Value, token));
        }

        // EmployeeId is forced on the payload before mapping, never trusted from the client —
        // same guard rail as CreateMyLeaveApplication. ApprovalStatus isn't on
        // CreateOverTimeApplication at all (only the admin Update DTO carries it), so the
        // entity default (ForApproval) already applies — set explicitly on the entity anyway
        // for clarity, since nothing on the wire could override it either way.
        [HttpPost("overtime-applications")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> CreateMyOvertimeApplication([FromBody] CreateOverTimeApplication payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.EmployeeId = employeeId.Value;
            var entity = _mapper.Map<OverTimeApplication>(payload);
            entity.ApprovalStatus = ApprovalStatus.ForApproval;
            await _overtimeApplicationService.AddAsync(entity, token);
            return Ok();
        }

        [HttpPatch("overtime-applications/{id:guid}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> WithdrawMyOvertimeApplication(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _overtimeApplicationService.WithdrawAsync(id, employeeId.Value, token);
            return Ok();
        }

        [HttpGet("travel-order-applications")]
        [ProducesResponseType(typeof(ResponseModel<List<TravelOrderApplication>>), 200)]
        public async Task<IActionResult> GetMyTravelOrderApplications(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            return Ok(await _travelOrderApplicationService.FindAllForEmployeeAsync(employeeId.Value, token));
        }

        // EmployeeId forced on the payload before mapping, same as Overtime — CreateTravelOrder
        // Application also has no ApprovalStatus field, so the entity default already applies;
        // set explicitly for clarity. Cost is zeroed on the payload too — the admin form already
        // treats it as finance-only and always sends 0 regardless of input, so self-service
        // filing follows the same convention rather than inventing a new one.
        [HttpPost("travel-order-applications")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> CreateMyTravelOrderApplication([FromBody] CreateTravelOrderApplication payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.EmployeeId = employeeId.Value;
            payload.Cost = 0;
            var entity = _mapper.Map<TravelOrderApplication>(payload);
            entity.ApprovalStatus = ApprovalStatus.ForApproval;
            await _travelOrderApplicationService.AddAsync(entity, token);
            return Ok();
        }

        [HttpPatch("travel-order-applications/{id:guid}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> WithdrawMyTravelOrderApplication(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _travelOrderApplicationService.WithdrawAsync(id, employeeId.Value, token);
            return Ok();
        }

        [HttpGet("pass-slip-applications")]
        [ProducesResponseType(typeof(ResponseModel<List<PassSlipApplication>>), 200)]
        public async Task<IActionResult> GetMyPassSlipApplications(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            return Ok(await _passSlipApplicationService.FindAllAsync(null, null, employeeId.Value, token));
        }

        // EmployeeId forced as above — PassSlipApplicationService.AddAsync already forces
        // ApprovalStatus.ForApproval itself, so this endpoint doesn't need to duplicate that.
        [HttpPost("pass-slip-applications")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> CreateMyPassSlipApplication([FromBody] CreatePassSlipApplication payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.EmployeeId = employeeId.Value;
            var entity = _mapper.Map<PassSlipApplication>(payload);
            await _passSlipApplicationService.AddAsync(entity, token);
            return Ok();
        }

        [HttpPatch("pass-slip-applications/{id:guid}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> WithdrawMyPassSlipApplication(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _passSlipApplicationService.WithdrawAsync(id, employeeId.Value, token);
            return Ok();
        }

        [HttpGet("profile-update-requests")]
        [ProducesResponseType(typeof(ResponseModel<List<EmployeeProfileUpdateRequestModel>>), 200)]
        public async Task<IActionResult> GetMyProfileUpdateRequests(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            return Ok(await _employeeProfileUpdateRequestService.FindAllAsync(employeeId.Value, token));
        }

        // EmployeeId forced as above — EmployeeProfileUpdateRequestService.AddAsync already forces
        // ApprovalStatus.ForApproval and snapshots Employee.UpdatedAt itself.
        [HttpPost("profile-update-requests")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> CreateMyProfileUpdateRequest([FromBody] CreateEmployeeProfileUpdateRequest payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.EmployeeId = employeeId.Value;
            var entity = _mapper.Map<EmployeeProfileUpdateRequest>(payload);
            await _employeeProfileUpdateRequestService.AddAsync(entity, token);
            return Ok();
        }

        [HttpPatch("profile-update-requests/{id:guid}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> WithdrawMyProfileUpdateRequest(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _employeeProfileUpdateRequestService.WithdrawAsync(id, employeeId.Value, token);
            return Ok();
        }

        [HttpGet("change-rest-day")]
        [ProducesResponseType(typeof(ResponseModel<List<RestDayRecordResponse>>), 200)]
        public async Task<IActionResult> GetMyChangeRestDayRequests(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            return Ok(await _changeRestDayService.FindList(new RestDayListFilter { EmployeeId = employeeId }, token));
        }

        // EmployeeId is resolved server-side and never trusted from the client, same guard rail
        // as every other create action here. RequestChangeOffAsync always files ForApproval —
        // see ChangeRestDayService for why that's safe against the DTR calculation path.
        [HttpPost("change-rest-day")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> CreateMyChangeRestDayRequest([FromBody] RequestChangeRestDay payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _changeRestDayService.RequestChangeOffAsync(
                employeeId.Value, payload.FromDay, payload.ToDay, payload.PayrollDateFrom, payload.PayrollDateTo, token);
            return Ok();
        }

        // Change Rest Day is batch-shaped (batchCode+employeeId, not a single id) -- see
        // ChangeRestDayService.WithdrawChangeOffAsync. employeeId is resolved server-side same as
        // everywhere else, so a caller can only ever match rows under their own batches.
        [HttpPatch("change-rest-day/{batchCode}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 404)]
        public async Task<IActionResult> WithdrawMyChangeRestDayRequest(string batchCode, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var withdrawn = await _changeRestDayService.WithdrawChangeOffAsync(batchCode, employeeId.Value, token);
            if (!withdrawn) return NotFound("No pending request found for that batch.");
            return Ok();
        }

        [HttpGet("loan-applications")]
        [ProducesResponseType(typeof(ResponseModel<List<DeductionApplicationModel>>), 200)]
        public async Task<IActionResult> GetMyLoanApplications(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var data = await _deductionApplicationService.FindAllForEmployeeAsync(employeeId.Value, token);
            return Ok(_mapper.Map<List<DeductionApplicationModel>>(data));
        }

        // EmployeeId is resolved server-side and never trusted from the client, same guard rail
        // as every other create action here. Always files ForApproval — an untrusted amortization
        // schedule must not reach payroll until HR approves it. See DeductionAplDtlService.LoadAsync.
        [HttpPost("loan-applications")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> CreateMyLoanApplication([FromBody] CreateDeductionApplication payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.EmployeeId = employeeId.Value;
            await _deductionApplicationService.AddAsync(payload, ApprovalStatus.ForApproval, token, isSelfService: true);
            return Ok();
        }

        [HttpPatch("loan-applications/{id:guid}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> WithdrawMyLoanApplication(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _deductionApplicationService.WithdrawAsync(id, employeeId.Value, token);
            return Ok();
        }

        // "My Cash Bond" — a savings-style progress view, kept separate from My Loan Ledger
        // above since a Cash Bond isn't debt the employee owes; it reuses the exact same
        // DeductionApplication/Breakdown shape though, so the frontend can share the same
        // amortization-schedule table component. See DeductionApplicationService.
        // FindCashBondForEmployeeAsync.
        [HttpGet("cash-bond")]
        [ProducesResponseType(typeof(ResponseModel<List<DeductionApplicationModel>>), 200)]
        public async Task<IActionResult> GetMyCashBond(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var data = await _deductionApplicationService.FindCashBondForEmployeeAsync(employeeId.Value, token);
            return Ok(_mapper.Map<List<DeductionApplicationModel>>(data));
        }

        // Scoped server-side to the caller's own EmployeeId via GetThirteenthMonthAsync's
        // employeeId filter — never computes or returns another employee's figures.
        [HttpGet("13th-month")]
        [ProducesResponseType(typeof(ResponseModel<ThirteenthMonthModel>), 200)]
        public async Task<IActionResult> GetMy13thMonth([FromQuery] int? year, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var data = await _payrollReportService.GetThirteenthMonthAsync(
                year ?? DateTime.UtcNow.Year, token, employeeId: employeeId.Value);
            return Ok(data.FirstOrDefault());
        }
    }
}
