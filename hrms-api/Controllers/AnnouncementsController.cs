using Onepunch.Common.Lib.Subscriptions;
using Hrms.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Core.Services.Announcements;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    // Communication > Announcements (publisher side). Every action is gated on the
    // "Announcements" catalog row; what an announcement's state allows is decided by
    // AnnouncementStateMachine and returned as allowedActions on each response.
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    [RequireFeature(EntitlementKeys.Announcements)]
    public class AnnouncementsController : ControllerBase
    {
        public const string View = "Announcements:View";
        public const string Create = "Announcements:Create";
        public const string Edit = "Announcements:Edit";
        public const string Delete = "Announcements:Delete";
        public const string Publish = "Announcements:Publish";

        private readonly AnnouncementService _announcements;
        private readonly AnnouncementReportService _reports;
        private readonly EmployeeService _employeeService;

        public AnnouncementsController(AnnouncementService announcements, AnnouncementReportService reports, EmployeeService employeeService)
        {
            _announcements = announcements;
            _reports = reports;
            _employeeService = employeeService;
        }

        // The caller's employee name when they have one, else their login email (e.g. an Owner
        // with no employee record).
        private async Task<AnnouncementActor> ResolveActorAsync(CancellationToken token)
        {
            var userId = User.GetRequiredUserId();
            var email = User.GetUserClaim("email");
            var employeeId = await _employeeService.ResolveEmployeeIdAsync(userId, email, token);
            var name = employeeId is { } id
                ? (await _employeeService.GetDisplayNamesAsync([id], token)).GetValueOrDefault(id)
                : null;
            return new AnnouncementActor(userId, string.IsNullOrWhiteSpace(name) ? email ?? "Unknown" : name);
        }

        [HttpGet]
        [RequirePermission(View)]
        [ProducesResponseType(typeof(ResponseModel<PaginatedResult<List<AnnouncementSummaryResponse>>>), 200)]
        public async Task<IActionResult> List([FromQuery] AnnouncementListQuery query, CancellationToken token) =>
            Ok(await _announcements.ListAsync(query, token));

        [HttpGet("{id:guid}")]
        [RequirePermission(View)]
        [ProducesResponseType(typeof(ResponseModel<AnnouncementDetailResponse>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token) =>
            Ok(await _announcements.GetAsync(id, token));

        [HttpGet("{id:guid}/stats")]
        [RequirePermission(View)]
        [ProducesResponseType(typeof(ResponseModel<AnnouncementStatsResponse>), 200)]
        public async Task<IActionResult> Stats(Guid id, CancellationToken token) =>
            Ok(await _reports.GetStatsAsync(id, token));

        [HttpGet("{id:guid}/recipients")]
        [RequirePermission(View)]
        [ProducesResponseType(typeof(ResponseModel<PaginatedResult<List<RecipientResponse>>>), 200)]
        public async Task<IActionResult> Recipients(Guid id, [FromQuery] RecipientListQuery query, CancellationToken token) =>
            Ok(await _reports.GetRecipientsAsync(id, query, token));

        // Read-only, but POST so the rule list travels in the body.
        [HttpPost("audience-preview")]
        [RequirePermission(View, Create, Edit)]
        [ProducesResponseType(typeof(ResponseModel<AudiencePreviewResponse>), 200)]
        public async Task<IActionResult> PreviewAudience([FromBody] AudiencePreviewRequest request, CancellationToken token) =>
            Ok(await _announcements.PreviewAudienceAsync(request, token));

        [HttpPost]
        [RequirePermission(Create)]
        [ProducesResponseType(typeof(ResponseModel<Guid>), 200)]
        public async Task<IActionResult> CreateDraft([FromBody] AnnouncementUpsertRequest request, CancellationToken token) =>
            Ok(await _announcements.CreateDraftAsync(request, await ResolveActorAsync(token), token));

        [HttpPost("{id:guid}/duplicate")]
        [RequirePermission(Create)]
        [ProducesResponseType(typeof(ResponseModel<Guid>), 200)]
        public async Task<IActionResult> Duplicate(Guid id, CancellationToken token) =>
            Ok(await _announcements.DuplicateAsync(id, await ResolveActorAsync(token), token));

        [HttpPut("{id:guid}")]
        [RequirePermission(Edit)]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Update(Guid id, [FromBody] AnnouncementUpsertRequest request, CancellationToken token)
        {
            await _announcements.UpdateAsync(id, request, token);
            return Ok();
        }

        [HttpPost("{id:guid}/publish")]
        [RequirePermission(Publish)]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> PublishNow(Guid id, CancellationToken token)
        {
            var published = await _announcements.PublishNowAsync(id, await ResolveActorAsync(token), token);
            if (!published) throw new ValidationException("This announcement was already published.");
            return Ok();
        }

        [HttpPost("{id:guid}/schedule")]
        [RequirePermission(Publish)]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Schedule(Guid id, [FromBody] ScheduleAnnouncementRequest request, CancellationToken token)
        {
            await _announcements.ScheduleAsync(id, request.PublishAtUtc, token);
            return Ok();
        }

        [HttpPost("{id:guid}/unschedule")]
        [RequirePermission(Publish)]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Unschedule(Guid id, CancellationToken token)
        {
            await _announcements.UnscheduleAsync(id, token);
            return Ok();
        }

        [HttpPost("{id:guid}/unpublish")]
        [RequirePermission(Publish)]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Unpublish(Guid id, [FromBody] UnpublishAnnouncementRequest request, CancellationToken token)
        {
            await _announcements.UnpublishAsync(id, request.Reason, await ResolveActorAsync(token), token);
            return Ok();
        }

        [HttpPost("{id:guid}/republish")]
        [RequirePermission(Publish)]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Republish(Guid id, CancellationToken token)
        {
            await _announcements.RepublishAsync(id, token);
            return Ok();
        }

        [HttpPost("{id:guid}/remind")]
        [EnableRateLimiting(RateLimitPolicies.NotifySend)]
        [RequirePermission(Publish)]
        [ProducesResponseType(typeof(ResponseModel<int>), 200)]
        public async Task<IActionResult> Remind(Guid id, [FromBody] RemindAnnouncementRequest request, CancellationToken token) =>
            Ok(await _announcements.RemindAsync(id, request.Target, token));

        [HttpPost("{id:guid}/pin")]
        [RequirePermission(Publish)]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Pin(Guid id, [FromBody] PinAnnouncementRequest request, CancellationToken token)
        {
            await _announcements.PinAsync(id, request.PinnedUntilUtc, token);
            return Ok();
        }

        [HttpPost("{id:guid}/unpin")]
        [RequirePermission(Publish)]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Unpin(Guid id, CancellationToken token)
        {
            await _announcements.UnpinAsync(id, token);
            return Ok();
        }

        [HttpPost("{id:guid}/archive")]
        [RequirePermission(Publish)]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Archive(Guid id, CancellationToken token)
        {
            await _announcements.ArchiveAsync(id, token);
            return Ok();
        }

        [HttpPost("{id:guid}/restore")]
        [RequirePermission(Publish)]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Restore(Guid id, CancellationToken token)
        {
            await _announcements.RestoreAsync(id, token);
            return Ok();
        }

        [HttpDelete("{id:guid}")]
        [RequirePermission(Delete)]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Remove(Guid id, CancellationToken token)
        {
            await _announcements.DeleteAsync(id, token);
            return Ok();
        }

        [HttpPost("{id:guid}/restore-deleted")]
        [RequirePermission(Delete)]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> RestoreDeleted(Guid id, CancellationToken token)
        {
            await _announcements.RestoreDeletedAsync(id, token);
            return Ok();
        }
    }
}
