using Onepunch.Common.Lib.Subscriptions;
using Hrms.Api.Filters;
using Hrms.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;
using Hrms.Api.Documents;
using Hrms.Api.Documents.Shared;
using Hrms.Api.Extensions;
using Hrms.Core.Services.Attachments;
using Hrms.Core.Services.UniformAllowance;
using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

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
    [RequireFeature(EntitlementKeys.PortalSelfService)]
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
        private readonly AttachmentService? _attachments;
        private readonly UniformAllowanceScheduleService? _uniformAllowanceSchedule;

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
            IMapper mapper,
            AttachmentService? attachments = null,
            UniformAllowanceScheduleService? uniformAllowanceSchedule = null)
        {
            _attachments = attachments;
            _uniformAllowanceSchedule = uniformAllowanceSchedule;
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

        // Coworkers celebrating today or within the next week who opted in to being greeted (no
        // DOB/age is exposed, only how many days away it is).
        [HttpGet("birthdays/upcoming")]
        [ProducesResponseType(typeof(List<CoworkerBirthdayModel>), 200)]
        public async Task<IActionResult> GetUpcomingBirthdays([FromServices] BirthdayService birthdays, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila")));
            var data = await birthdays.GetUpcomingForCoworkersAsync(today, BirthdayService.CoworkerLookAheadDays, token);
            return Ok(data.Where(c => c.Id != employeeId.Value).ToList());
        }

        public record BirthdaySharingRequest(bool Share);

        // The employee's own opt-in; the id is always resolved from the JWT, never the client.
        [HttpPut("birthday-sharing")]
        public async Task<IActionResult> SetBirthdaySharing(
            [FromBody] BirthdaySharingRequest request, [FromServices] BirthdayService birthdays, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();
            return await birthdays.SetShareAsync(employeeId.Value, request.Share, token) ? NoContent() : NotFound();
        }

        private static DateOnly PhToday() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila")));

        // Which optional My Pay & Benefits pages the company switched on (Company Settings >
        // Payroll Settings), so the portal menu only lists those.
        [HttpGet("portal-visibility")]
        [ProducesResponseType(typeof(ResponseModel<PortalVisibilityModel>), 200)]
        public async Task<IActionResult> GetPortalVisibility([FromServices] PortalVisibilityService visibility) =>
            Ok(await visibility.GetAsync());

        // Which Coworkers widgets the company switched on, so the page only shows those tabs.
        [HttpGet("coworkers/settings")]
        public async Task<IActionResult> GetCoworkerSettings([FromServices] CoworkerService coworkers)
        {
            var w = await coworkers.GetWidgetsAsync();
            return Ok(new CoworkerSettingsModel
            {
                ShowNewHires = w.ShowNewHires,
                ShowAnniversaries = w.ShowAnniversaries,
                ShowWhosOut = w.ShowWhosOut,
                ShowDirectory = w.ShowDirectory,
            });
        }

        [HttpGet("coworkers/new-hires")]
        public async Task<IActionResult> GetNewHires([FromServices] CoworkerService coworkers, CancellationToken token) =>
            Ok(await coworkers.GetNewHiresAsync(PhToday(), token));

        [HttpGet("coworkers/anniversaries")]
        public async Task<IActionResult> GetAnniversaries([FromServices] CoworkerService coworkers, CancellationToken token) =>
            Ok(await coworkers.GetAnniversariesAsync(PhToday(), token));

        [HttpGet("coworkers/out-today")]
        public async Task<IActionResult> GetOutToday([FromServices] CoworkerService coworkers, CancellationToken token) =>
            Ok(await coworkers.GetOutTodayAsync(PhToday(), token));

        [HttpGet("coworkers/directory")]
        public async Task<IActionResult> GetDirectory(
            [FromServices] CoworkerService coworkers, [FromQuery] string? q, [FromQuery] Guid? departmentId,
            [FromQuery] int page = 1, CancellationToken token = default) =>
            Ok(await coworkers.SearchDirectoryAsync(q, departmentId, page, token));

        [HttpGet("coworkers/org-chart")]
        public async Task<IActionResult> GetOrgChart([FromServices] CoworkerService coworkers, CancellationToken token) =>
            Ok(await coworkers.GetOrgChartAsync(token));

        public record ContactSharingRequest(bool Share);

        // The employee's own opt-in to show their email/contact in the directory.
        [HttpPut("contact-sharing")]
        public async Task<IActionResult> SetContactSharing(
            [FromBody] ContactSharingRequest request, [FromServices] CoworkerService coworkers, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();
            return await coworkers.SetShareContactAsync(employeeId.Value, request.Share, token) ? NoContent() : NotFound();
        }

        [RequireFeature(EntitlementKeys.PayrollCore)]
        [HttpGet("payrolls")]
        [ProducesResponseType(typeof(ResponseModel<List<Payroll>>), 200)]
        public async Task<IActionResult> GetMyPayrolls([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var fromDate = DateOnly.FromDateTime(from);
            var toDate = DateOnly.FromDateTime(to);
            var data = await _payrollService.GetAsync(fromDate, toDate, employeeId, null, null, null, token);
            return Ok(new { data, total = data.Count });
        }

        [RequireFeature(EntitlementKeys.PayrollCore)]
        [HttpGet("payrolls/{id:guid}/print")]
        [EnableRateLimiting(RateLimitPolicies.DocumentRender)]
        public async Task<IActionResult> PrintMyPayslip(Guid id, [FromServices] IncomeAplDtlService otherIncomeService,
            [FromServices] Hrms.Core.Services.Storage.CompanyLetterheadService letterhead,
            CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var payroll = await _payrollService.FineOneAsync(id, token);
            if (payroll == null || payroll.EmployeeId != employeeId) return NotFound();

            var employee = await _employeeService.GetFullByIdAsync(payroll.EmployeeId, token);
            if (employee == null) return NotFound();
            var company = await _companyService.FineOneAsync(token);
            // Same layout and Other Income rules as PayrollsController.PrintPayslip.
            var otherIncome = new PayslipOtherIncome(await otherIncomeService.GetPayslipLinesAsync(payroll.Id, token));
            var logo = await letterhead.LoadLogoAsync(company, token);
            var document = PayslipDocumentFactory.Create(payroll, employee, company, otherIncome, logo);
            var bytes = document.GeneratePdf();
            return File(bytes, "application/pdf", $"payslip-{employee.EmployeeNo}-{payroll.PayPeriodStart:yyyyMMdd}.pdf");
        }

        // Setup > Payslip/13th Month/Last Pay > Received by Employee — informational only,
        // records that the employee has received/viewed this document; nothing else in the
        // system reads or gates on it. Covers all three document types uniformly since
        // Payslip/13th Month/Last Pay are all just Payroll rows differentiated by PayrollType.
        [RequireFeature(EntitlementKeys.PayrollCore)]
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
            var result = await _dtrCalcService.GetDTRInfoAsync<ColumnarLogModel>(payload, ProcessorType.Incomplete, token, IncludeNullResponse.Include, processOnlyPairedAtt: false);
            return Ok(result);
        }

        // Portal counterpart of Timekeeping's Raw Logs (DailyRecordsController.GenerateRawColumnarView),
        // always scoped to the caller's own employee record.
        [HttpGet("raw-attendance")]
        [ProducesResponseType(typeof(ResponseModel<ObjectCollection<ColumnarLogModel>>), 200)]
        public async Task<IActionResult> GetMyRawAttendance([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var payload = new DTRRequestPayload { FromDate = DateOnly.FromDateTime(from), ToDate = DateOnly.FromDateTime(to), EmployeeId = employeeId };
            var result = await _dtrCalcService.GetDTRInfoAsync<ColumnarLogModel>(payload, ProcessorType.RawColumnarLog, token, IncludeNullResponse.Include,
                processOnlyPairedAtt: false, removedoublePunch: false);
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

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpGet("leave-credits")]
        [ProducesResponseType(typeof(ResponseModel<List<LeaveCreditsBalanceModel>>), 200)]
        public async Task<IActionResult> GetMyLeaveCredits([FromQuery] int? year, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            return Ok(await _leaveLedgerService.GetBalancesForEmployeeAsync(employeeId.Value, year ?? DateTime.UtcNow.Year, token));
        }

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
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
        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpPost("leave-applications")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> CreateMyLeaveApplication([FromBody] CreateLeaveApplication payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.EmployeeId = employeeId.Value;
            payload.ApprovalStatus = ApprovalStatus.ForApproval;
            var intake = _attachments == null ? null : new AttachmentIntake(_attachments, User.ToAttachmentCaller(employeeId));
            await _leaveApplicationService.AddAsync(payload, token, isSelfService: true, attachments: intake);
            await _leaveApplicationService.CommitChangesAsync(token);
            return Ok();
        }

        // Ownership + pending-only guard lives in LeaveApplicationService.WithdrawAsync -- it
        // throws NotFoundException (also for "belongs to someone else", so a caller can't probe
        // whether another employee's application id exists) or InvalidOperationException (not
        // ForApproval anymore), both handled by the standard exception pipeline.
        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpPatch("leave-applications/{id:guid}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> WithdrawMyLeaveApplication(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _leaveApplicationService.WithdrawAsync(id, employeeId.Value, token);
            return Ok();
        }

        // Self-service edit of the caller's own still-pending application. Ownership, the
        // pending-only rule, the portal filing policy, and the "back to the first approver if
        // anyone already approved" restart all live in LeaveApplicationService.UpdateOwnAsync.
        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpPut("leave-applications/{id:guid}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> UpdateMyLeaveApplication(Guid id, [FromBody] UpdateLeaveApplication payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.Id = id;
            payload.EmployeeId = employeeId.Value;
            await _leaveApplicationService.UpdateOwnAsync(payload, employeeId.Value, token);
            return Ok();
        }

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpGet("overtime-applications")]
        [ProducesResponseType(typeof(ResponseModel<List<OvertimeApplicationModel>>), 200)]
        public async Task<IActionResult> GetMyOvertimeApplications(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            // Mapped to the DTO, never the entity: its virtual Employee navigation let the JSON
            // serializer lazy-load Employee -> Manager -> DirectReports -> ... per row -- the
            // RAM spike and 30s timeout on My Overtime Applications (same root cause as me/employee).
            var data = await _overtimeApplicationService.FindAllForEmployeeAsync(employeeId.Value, token);
            return Ok(_mapper.Map<List<OvertimeApplicationModel>>(data));
        }

        // EmployeeId is forced on the payload before mapping, never trusted from the client —
        // same guard rail as CreateMyLeaveApplication. ApprovalStatus isn't on
        // CreateOverTimeApplication at all (only the admin Update DTO carries it), so the
        // entity default (ForApproval) already applies — set explicitly on the entity anyway
        // for clarity, since nothing on the wire could override it either way.
        [RequireFeature(EntitlementKeys.ApplicationsCore)]
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

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpPatch("overtime-applications/{id:guid}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> WithdrawMyOvertimeApplication(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _overtimeApplicationService.WithdrawAsync(id, employeeId.Value, token);
            return Ok();
        }

        // See UpdateMyLeaveApplication -- same rules, enforced in OvertimeApplicationService.UpdateOwnAsync.
        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpPut("overtime-applications/{id:guid}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> UpdateMyOvertimeApplication(Guid id, [FromBody] UpdateOvertimeApplication payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.Id = id;
            payload.EmployeeId = employeeId.Value;
            await _overtimeApplicationService.UpdateOwnAsync(payload, employeeId.Value, token);
            return Ok();
        }

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpGet("travel-order-applications")]
        [ProducesResponseType(typeof(ResponseModel<List<TravelOrderApplicationModel>>), 200)]
        public async Task<IActionResult> GetMyTravelOrderApplications(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            // DTO, not the entity -- see GetMyOvertimeApplications.
            var data = await _travelOrderApplicationService.FindAllForEmployeeAsync(employeeId.Value, token);
            return Ok(_mapper.Map<List<TravelOrderApplicationModel>>(data));
        }

        // EmployeeId forced on the payload before mapping, same as Overtime — CreateTravelOrder
        // Application also has no ApprovalStatus field, so the entity default already applies;
        // set explicitly for clarity. Cost is zeroed on the payload too — the admin form already
        // treats it as finance-only and always sends 0 regardless of input, so self-service
        // filing follows the same convention rather than inventing a new one.
        [RequireFeature(EntitlementKeys.ApplicationsCore)]
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
            var intake = _attachments == null ? null : new AttachmentIntake(_attachments, User.ToAttachmentCaller(employeeId));
            await _travelOrderApplicationService.AddAsync(entity, token, intake, payload.AttachmentIds);
            return Ok();
        }

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpPatch("travel-order-applications/{id:guid}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> WithdrawMyTravelOrderApplication(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _travelOrderApplicationService.WithdrawAsync(id, employeeId.Value, token);
            return Ok();
        }

        // See UpdateMyLeaveApplication. Cost stays zeroed, same convention as filing.
        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpPut("travel-order-applications/{id:guid}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> UpdateMyTravelOrderApplication(Guid id, [FromBody] UpdateTravelOrderApplication payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.Id = id;
            payload.EmployeeId = employeeId.Value;
            payload.Cost = 0;
            await _travelOrderApplicationService.UpdateOwnAsync(payload, employeeId.Value, token);
            return Ok();
        }

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpGet("pass-slip-applications")]
        [ProducesResponseType(typeof(ResponseModel<List<PassSlipApplicationModel>>), 200)]
        public async Task<IActionResult> GetMyPassSlipApplications(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            // DTO, not the entity -- the Include'd Employee would otherwise be serialized with
            // its whole lazy-loaded graph. See GetMyOvertimeApplications.
            var data = await _passSlipApplicationService.FindAllAsync(null, null, employeeId.Value, token);
            return Ok(_mapper.Map<List<PassSlipApplicationModel>>(data));
        }

        // EmployeeId forced as above — PassSlipApplicationService.AddAsync already forces
        // ApprovalStatus.ForApproval itself, so this endpoint doesn't need to duplicate that.
        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpPost("pass-slip-applications")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> CreateMyPassSlipApplication([FromBody] CreatePassSlipApplication payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.EmployeeId = employeeId.Value;
            var entity = _mapper.Map<PassSlipApplication>(payload);
            var intake = _attachments == null ? null : new AttachmentIntake(_attachments, User.ToAttachmentCaller(employeeId));
            await _passSlipApplicationService.AddAsync(entity, token, intake, payload.AttachmentIds);
            return Ok();
        }

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpPatch("pass-slip-applications/{id:guid}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> WithdrawMyPassSlipApplication(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _passSlipApplicationService.WithdrawAsync(id, employeeId.Value, token);
            return Ok();
        }

        // See UpdateMyLeaveApplication -- enforced in PassSlipApplicationService.UpdateOwnAsync.
        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpPut("pass-slip-applications/{id:guid}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> UpdateMyPassSlipApplication(Guid id, [FromBody] UpdatePassSlipApplication payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.Id = id;
            payload.EmployeeId = employeeId.Value;
            await _passSlipApplicationService.UpdateOwnAsync(payload, employeeId.Value, token);
            return Ok();
        }

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
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
        [RequireFeature(EntitlementKeys.ApplicationsCore)]
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

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
        [HttpPatch("profile-update-requests/{id:guid}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> WithdrawMyProfileUpdateRequest(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _employeeProfileUpdateRequestService.WithdrawAsync(id, employeeId.Value, token);
            return Ok();
        }

        [RequireFeature(EntitlementKeys.ApplicationsCore)]
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
        [RequireFeature(EntitlementKeys.ApplicationsCore)]
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
        [RequireFeature(EntitlementKeys.ApplicationsCore)]
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

        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
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
        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
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

        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
        [HttpPatch("loan-applications/{id:guid}/withdraw")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> WithdrawMyLoanApplication(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            await _deductionApplicationService.WithdrawAsync(id, employeeId.Value, token);
            return Ok();
        }

        // See UpdateMyLeaveApplication -- enforced in DeductionApplicationService.UpdateOwnAsync
        // (which also re-checks the deduction type is still employee-fileable).
        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
        [HttpPut("loan-applications/{id:guid}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> UpdateMyLoanApplication(Guid id, [FromBody] UpdateDeductionApplication payload, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            payload.Id = id;
            payload.EmployeeId = employeeId.Value;
            await _deductionApplicationService.UpdateOwnAsync(payload, employeeId.Value, token);
            return Ok();
        }

        // "My Cash Bond" — Cash Bond is now a flat, recurring deduction sourced straight from
        // Employee.CashBond (see CashBondDeductionPolicy), not debt the employee owes -- shows
        // the current per-run rate plus every payroll run that's actually collected it so far.
        // See PayrollReportService.GetMyCashBondAsync.
        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
        [HttpGet("cash-bond")]
        [ProducesResponseType(typeof(ResponseModel<MyCashBondModel>), 200)]
        public async Task<IActionResult> GetMyCashBond([FromServices] PortalVisibilityService visibility, CancellationToken token)
        {
            if (!(await visibility.GetAsync()).ShowCashBond) return NotFound();
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var data = await _payrollReportService.GetMyCashBondAsync(employeeId.Value, token);
            return Ok(data);
        }

        // "My Uniform Allowance" — the caller's balance, every movement, recent credit decisions
        // (including skipped ones with the reason), the next credit dates and the rules behind them.
        [RequireFeature(EntitlementKeys.PayrollLoansIncome)]
        [HttpGet("uniform-allowance")]
        [ProducesResponseType(typeof(ResponseModel<MyUniformAllowanceModel>), 200)]
        public async Task<IActionResult> GetMyUniformAllowance([FromServices] PortalVisibilityService visibility, CancellationToken token)
        {
            if (!(await visibility.GetAsync()).ShowUniformAllowance) return NotFound();
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null || _uniformAllowanceSchedule == null) return NotFound();

            return Ok(await _uniformAllowanceSchedule.GetMineAsync(employeeId.Value, token));
        }

        // Scoped server-side to the caller's own EmployeeId via GetThirteenthMonthAsync's
        // employeeId filter — never computes or returns another employee's figures.
        [RequireFeature(EntitlementKeys.PayrollCore)]
        [HttpGet("13th-month")]
        [ProducesResponseType(typeof(ResponseModel<ThirteenthMonthModel>), 200)]
        public async Task<IActionResult> GetMy13thMonth(
            [FromQuery] int? year, [FromServices] PortalVisibilityService visibility, CancellationToken token)
        {
            if (!(await visibility.GetAsync()).ShowThirteenthMonth) return NotFound();
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();

            var data = await _payrollReportService.GetThirteenthMonthAsync(
                year ?? DateTime.UtcNow.Year, token, employeeId: employeeId.Value);
            return Ok(data.FirstOrDefault());
        }
    }
}
