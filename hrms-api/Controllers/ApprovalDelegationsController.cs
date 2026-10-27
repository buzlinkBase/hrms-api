using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Core.Services;
using Hrms.Core.Services.Approvals;
using Hrms.Domain;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

// "While I'm away" delegation. Self-service first (like notification preferences): each person
// manages only their own, taken from their token. The admin endpoints below let an owner/admin set
// one up for someone else.
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class ApprovalDelegationsController : ControllerBase
{
    private readonly ApprovalDelegationService _service;
    private readonly EmployeeService _employeeService;

    public ApprovalDelegationsController(ApprovalDelegationService service, EmployeeService employeeService)
    {
        _service = service;
        _employeeService = employeeService;
    }

    private async Task<Guid> CallerEmployeeIdAsync(CancellationToken token) =>
        await _employeeService.ResolveEmployeeIdAsync(User.GetRequiredUserId(), User.GetUserClaim("email"), token)
        ?? throw new AccountNotLinkedException("approval delegation can't be resolved");

    private static ApprovalDelegation ToEntity(ApprovalDelegationRequest payload, Guid delegatorEmployeeId) => new()
    {
        DelegatorEmployeeId = delegatorEmployeeId,
        DelegateEmployeeId = payload.DelegateEmployeeId,
        FromDate = payload.FromDate,
        ToDate = payload.ToDate,
        ApplicationType = payload.ApplicationType,
        OnlyWhileOnLeave = payload.OnlyWhileOnLeave,
        Reason = string.IsNullOrWhiteSpace(payload.Reason) ? null : payload.Reason.Trim(),
    };

    private async Task<List<ApprovalDelegationResponse>> ToResponsesAsync(
        IReadOnlyCollection<ApprovalDelegation> delegations, CancellationToken token)
    {
        var names = await _employeeService.GetDisplayNamesAsync(
            delegations.SelectMany(d => new[] { d.DelegatorEmployeeId, d.DelegateEmployeeId }), token);
        var today = PhTime.Today(DateTime.UtcNow);
        return delegations.Select(d => new ApprovalDelegationResponse
        {
            Id = d.Id,
            DelegatorEmployeeId = d.DelegatorEmployeeId,
            DelegatorName = names.GetValueOrDefault(d.DelegatorEmployeeId),
            DelegateEmployeeId = d.DelegateEmployeeId,
            DelegateName = names.GetValueOrDefault(d.DelegateEmployeeId),
            FromDate = d.FromDate,
            ToDate = d.ToDate,
            ApplicationType = d.ApplicationType,
            OnlyWhileOnLeave = d.OnlyWhileOnLeave,
            IsActive = d.IsActive,
            Reason = d.Reason,
            IsInWindow = d.IsActive && ApprovalDelegationRules.InWindow(d, today),
        }).ToList();
    }

    /// <summary>What the caller has delegated away.</summary>
    [HttpGet("api/v{version:apiVersion}/me/approval-delegations")]
    [ProducesResponseType(typeof(ResponseModel<List<ApprovalDelegationResponse>>), 200)]
    public async Task<IActionResult> GetMine(CancellationToken token)
    {
        var employeeId = await CallerEmployeeIdAsync(token);
        return Ok(await ToResponsesAsync(await _service.ListForDelegatorAsync(employeeId, token), token));
    }

    /// <summary>What others have delegated to the caller -- so they know whose requests they may act on.</summary>
    [HttpGet("api/v{version:apiVersion}/me/approval-delegations/to-me")]
    [ProducesResponseType(typeof(ResponseModel<List<ApprovalDelegationResponse>>), 200)]
    public async Task<IActionResult> GetToMe(CancellationToken token)
    {
        var employeeId = await CallerEmployeeIdAsync(token);
        return Ok(await ToResponsesAsync(await _service.ListToDelegateAsync(employeeId, token), token));
    }

    /// <summary>People the caller can delegate to (they must have a login), found by name or employee number.</summary>
    [HttpGet("api/v{version:apiVersion}/me/approval-delegations/candidates")]
    [ProducesResponseType(typeof(ResponseModel<List<ApprovalDelegationService.DelegateCandidate>>), 200)]
    public async Task<IActionResult> GetCandidates([FromQuery] string? search, CancellationToken token)
    {
        var employeeId = await CallerEmployeeIdAsync(token);
        return Ok(await _service.FindCandidatesAsync(employeeId, search, token));
    }

    [HttpPost("api/v{version:apiVersion}/me/approval-delegations")]
    [ProducesResponseType(typeof(ResponseModel<ApprovalDelegationResponse>), 200)]
    public async Task<IActionResult> CreateMine([FromBody] ApprovalDelegationRequest payload, CancellationToken token)
    {
        var employeeId = await CallerEmployeeIdAsync(token);
        var created = await _service.CreateDelegationAsync(ToEntity(payload, employeeId), token);
        return Ok((await ToResponsesAsync([created], token)).Single());
    }

    [HttpPost("api/v{version:apiVersion}/me/approval-delegations/{id:guid}/deactivate")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> DeactivateMine(Guid id, CancellationToken token)
    {
        var employeeId = await CallerEmployeeIdAsync(token);
        await _service.DeactivateAsync(id, onlyForDelegator: employeeId, token);
        return Ok();
    }

    // ---- admin ----

    [HttpGet("api/v{version:apiVersion}/approval-delegations")]
    [RequirePermission("Approval Workflows:View")]
    [ProducesResponseType(typeof(ResponseModel<List<ApprovalDelegationResponse>>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken token) =>
        Ok(await ToResponsesAsync(await _service.ListAllAsync(token), token));

    [HttpPost("api/v{version:apiVersion}/approval-delegations")]
    [RequirePermission("Approval Workflows:Edit")]
    [ProducesResponseType(typeof(ResponseModel<ApprovalDelegationResponse>), 200)]
    public async Task<IActionResult> Create([FromBody] ApprovalDelegationRequest payload, CancellationToken token)
    {
        var delegator = payload.DelegatorEmployeeId
            ?? throw new InvalidOperationException("Choose who is delegating.");
        var created = await _service.CreateDelegationAsync(ToEntity(payload, delegator), token);
        return Ok((await ToResponsesAsync([created], token)).Single());
    }

    [HttpPost("api/v{version:apiVersion}/approval-delegations/{id:guid}/deactivate")]
    [RequirePermission("Approval Workflows:Edit")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken token)
    {
        await _service.DeactivateAsync(id, onlyForDelegator: null, token);
        return Ok();
    }
}
