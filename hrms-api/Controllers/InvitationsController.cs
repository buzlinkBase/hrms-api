using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Core.Services.Invitations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

/// <summary>
/// The batch invite modal's lookups against this company's records. The invitations themselves
/// are sent by AuthApi (InvitationController); these tie each email to the employee or client it
/// is for. Read-only, so POST only to carry up to 100 emails in the body.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[Authorize]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
public class InvitationsController : ControllerBase
{
    private readonly InviteMatchService _service;

    public InvitationsController(InviteMatchService service) => _service = service;

    /// <summary>Employee mode: the employee each email belongs to, or why it can't be invited as one.</summary>
    [HttpPost("employee-match")]
    [RequirePermission("Users:Invite")]
    [ProducesResponseType(typeof(ResponseModel<List<InviteMatchResult>>), 200)]
    public async Task<IActionResult> MatchEmployees([FromBody] InviteMatchRequest payload, CancellationToken token) =>
        Ok(await _service.MatchEmployeesAsync(payload.Emails, token));

    /// <summary>Client mode: which emails can become logins for this client.</summary>
    [HttpPost("clients/{clientId:guid}/match")]
    [RequirePermission("Users:Invite")]
    [ProducesResponseType(typeof(ResponseModel<List<InviteMatchResult>>), 200)]
    public async Task<IActionResult> MatchClientLogins(Guid clientId, [FromBody] InviteMatchRequest payload, CancellationToken token) =>
        Ok(await _service.MatchClientLoginsAsync(clientId, payload.Emails, token));
}
