using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Core.Services.Announcements;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    // The signed-in employee's own announcements. Like MeController, the caller's EmployeeId is
    // resolved server-side from the token and never accepted from the client, so any
    // authenticated user may call it. A caller with no employee record (e.g. an Owner account)
    // simply has none -- the bell asks for the unread count on every page, so that's an empty
    // result, not an error.
    [Route("api/v{version:apiVersion}/me/announcements")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class MyAnnouncementsController : ControllerBase
    {
        private readonly MyAnnouncementService _mine;
        private readonly EmployeeService _employeeService;

        public MyAnnouncementsController(MyAnnouncementService mine, EmployeeService employeeService)
        {
            _mine = mine;
            _employeeService = employeeService;
        }

        private Task<Guid?> ResolveMyEmployeeIdAsync(CancellationToken token) =>
            _employeeService.ResolveEmployeeIdAsync(User.GetRequiredUserId(), User.GetUserClaim("email"), token);

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<PaginatedResult<List<MyAnnouncementListItem>>>), 200)]
        public async Task<IActionResult> List([FromQuery] MyAnnouncementQuery query, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null)
            {
                return Ok(new PaginatedResult<List<MyAnnouncementListItem>>
                {
                    Data = [],
                    MetaData = new PaginationMetaData(0, 1, query.Limit),
                });
            }
            return Ok(await _mine.GetMineAsync(employeeId.Value, query, token));
        }

        [HttpGet("unread-count")]
        [ProducesResponseType(typeof(ResponseModel<int>), 200)]
        public async Task<IActionResult> UnreadCount(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            return Ok(employeeId == null ? 0 : await _mine.GetUnreadCountAsync(employeeId.Value, token));
        }

        [HttpGet("pending-acknowledgement")]
        [ProducesResponseType(typeof(ResponseModel<List<MyAnnouncementListItem>>), 200)]
        public async Task<IActionResult> PendingAcknowledgement(CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            return Ok(employeeId == null ? [] : await _mine.GetPendingAcknowledgementsAsync(employeeId.Value, token));
        }

        // Opening one records the view.
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ResponseModel<MyAnnouncementDetail>), 200)]
        public async Task<IActionResult> Open(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();
            return Ok(await _mine.OpenAsync(employeeId.Value, id, token));
        }

        [HttpPost("{id:guid}/acknowledge")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Acknowledge(Guid id, CancellationToken token)
        {
            var employeeId = await ResolveMyEmployeeIdAsync(token);
            if (employeeId == null) return NotFound();
            await _mine.AcknowledgeAsync(employeeId.Value, id, token);
            return Ok();
        }
    }
}
