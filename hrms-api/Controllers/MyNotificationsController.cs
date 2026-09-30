using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Core.Services.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

/// <summary>
/// The caller's own notification bell: newest first, the unread count, and marking entries read.
/// The caller is always taken from their token (sub + email claims), never from the request.
/// </summary>
[Route("api/v{version:apiVersion}/me/notifications")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class MyNotificationsController : ControllerBase
{
    private readonly UserNotificationService _service;

    public MyNotificationsController(UserNotificationService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(ResponseModel<UserNotificationPage>), 200)]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool unreadOnly = false, CancellationToken token = default) =>
        Ok(await _service.GetMineAsync(User.GetRequiredUserId(), User.GetUserClaim("email"), page, pageSize, unreadOnly, token));

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken token) =>
        await _service.MarkReadAsync(User.GetRequiredUserId(), User.GetUserClaim("email"), id, token)
            ? NoContent()
            : NotFound();

    [HttpPost("read-all")]
    [ProducesResponseType(typeof(ResponseModel<int>), 200)]
    public async Task<IActionResult> MarkAllRead(CancellationToken token) =>
        Ok(await _service.MarkAllReadAsync(User.GetRequiredUserId(), User.GetUserClaim("email"), token));
}
