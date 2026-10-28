using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Core.Services;
using Hrms.Core.Services.Approvals;
using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Onepunch.Common.Lib.Security;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Api.Controllers;

// The signed-in person's approval inbox: requests waiting on them directly or through a delegation,
// and the approve/decline actions. Unlike each module's own endpoint this one has no {Type}:Approve
// attribute -- a person acting FOR a delegator doesn't hold it -- so the access check is explicit:
// direct approvers still need the permission, delegated ones don't, and the approval engine checks
// eligibility again when the module service runs it.
[Route("api/v{version:apiVersion}/me/approvals")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class MyApprovalsController : ControllerBase
{
    private readonly MyApprovalsService _service;
    private readonly EmployeeService _employeeService;

    public MyApprovalsController(MyApprovalsService service, EmployeeService employeeService)
    {
        _service = service;
        _employeeService = employeeService;
    }

    private async Task<Guid> CallerEmployeeIdAsync(CancellationToken token) =>
        await _employeeService.ResolveEmployeeIdAsync(User.GetRequiredUserId(), User.GetUserClaim("email"), token)
        ?? throw new AccountNotLinkedException("approvals can't be resolved");

    [HttpGet("pending")]
    [ProducesResponseType(typeof(ResponseModel<List<MyApprovalResponse>>), 200)]
    public async Task<IActionResult> GetPending(CancellationToken token)
    {
        var employeeId = await CallerEmployeeIdAsync(token);
        var items = await _service.GetPendingAsync(employeeId, prefix => User.HasAnyPermission($"{prefix}:Approve"), token);
        return Ok(items.Select(ToResponse).ToList());
    }

    // Still being approved but on someone else's step: what the caller is part of and can follow, not
    // act on. Owners and Admins see every request in flight.
    [HttpGet("in-progress")]
    [ProducesResponseType(typeof(ResponseModel<List<MyApprovalResponse>>), 200)]
    public async Task<IActionResult> GetInProgress(CancellationToken token)
    {
        var employeeId = await CallerEmployeeIdAsync(token);
        var items = await _service.GetInProgressAsync(
            employeeId, prefix => User.HasAnyPermission($"{prefix}:Approve"), token, seesEverything: User.IsOwnerOrAdmin());
        return Ok(items.Select(ToResponse).ToList());
    }

    private const int MaxHistorySpanDays = 366;
    private const int MaxHistoryRows = 500;

    // Finished requests the caller decided (themself or through a delegate) between two dates,
    // newest first. Defaults to the current month; the range is capped so one call stays small.
    [HttpGet("history")]
    [ProducesResponseType(typeof(ResponseModel<List<MyApprovalHistoryResponse>>), 200)]
    public async Task<IActionResult> GetHistory([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken token)
    {
        var today = PhTime.Today(DateTime.UtcNow);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? start.AddMonths(1).AddDays(-1);
        if (end < start) return BadRequest("The end date can't be before the start date.");
        if (end.DayNumber - start.DayNumber >= MaxHistorySpanDays) return BadRequest("Choose a range of one year or less.");

        var employeeId = await CallerEmployeeIdAsync(token);
        var items = await _service.GetHistoryAsync(employeeId, start, end, MaxHistoryRows, token);
        return Ok(items.Select(i => new MyApprovalHistoryResponse
        {
            ApplicationType = i.ApplicationType,
            ApplicationId = i.ApplicationId,
            ApplicantEmployeeId = i.ApplicantEmployeeId,
            ApplicantName = i.ApplicantName,
            Summary = i.Summary,
            FiledAt = PhTime.AsUtc(i.FiledAt),
            FinalStatus = i.FinalStatus,
            MyDecision = i.MyDecision,
            DecidedAt = PhTime.AsUtc(i.DecidedAt),
            Note = i.Note,
            OnBehalfOfName = i.OnBehalfOfName,
            DecidedByName = i.DecidedByName,
            Trail = i.Trail.Select(t => new MyApprovalTrailResponse
            {
                StepNumber = t.StepNumber,
                Action = t.Action,
                ApproverName = t.ApproverName,
                OnBehalfOfName = t.OnBehalfOfName,
                At = PhTime.AsUtc(t.At),
                Note = t.Note,
            }).ToList(),
        }).ToList());
    }

    private static MyApprovalResponse ToResponse(MyApprovalItem i) => new()
    {
        ApplicationType = i.ApplicationType,
        ApplicationId = i.ApplicationId,
        ApplicantEmployeeId = i.ApplicantEmployeeId,
        ApplicantName = i.ApplicantName,
        Summary = i.Summary,
        FiledAt = PhTime.AsUtc(i.FiledAt),
        StepNumber = i.StepNumber,
        TotalSteps = i.TotalSteps,
        FlowName = i.FlowName,
        NoteRequirement = i.NoteRequirement,
        OnBehalfOfEmployeeId = i.OnBehalfOfEmployeeId,
        OnBehalfOfName = i.OnBehalfOfName,
        CurrentApproverLabel = i.CurrentApproverLabel,
    };

    [HttpPost("{applicationType}/{applicationId:guid}/approve")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public Task<IActionResult> Approve(ApprovalApplicationType applicationType, Guid applicationId, [FromBody] ApprovalActionRequest? body, CancellationToken token) =>
        ActAsync(applicationType, applicationId, approve: true, body?.Note, token);

    [HttpPost("{applicationType}/{applicationId:guid}/decline")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public Task<IActionResult> Decline(ApprovalApplicationType applicationType, Guid applicationId, [FromBody] ApprovalActionRequest? body, CancellationToken token) =>
        ActAsync(applicationType, applicationId, approve: false, body?.Note, token);

    private async Task<IActionResult> ActAsync(
        ApprovalApplicationType type, Guid applicationId, bool approve, string? note, CancellationToken token)
    {
        var adapter = _service.AdapterFor(type);
        if (adapter == null) return NotFound();

        var employeeId = await CallerEmployeeIdAsync(token);
        var hasOverride = User.IsOwnerOrAdmin();
        var standing = await _service.GetStandingAsync(type, applicationId, employeeId, token);

        // Acting as themself needs the type's Approve permission, exactly as on the module's own
        // screen. Acting for a delegator is what delegation grants, so it doesn't.
        // Eligibility itself is enforced by the engine, which names who the step is waiting on.
        var allowed = standing.Standing.GrantsAccess || User.HasAnyPermission($"{adapter.PermissionPrefix}:Approve");
        if (!allowed) throw new ForbiddenException("You don't have permission to approve or decline this kind of request.");

        if (approve) await adapter.ApproveAsync(applicationId, employeeId, hasOverride, note, token);
        else await adapter.DeclineAsync(applicationId, employeeId, hasOverride, note, token);
        return Ok();
    }
}
