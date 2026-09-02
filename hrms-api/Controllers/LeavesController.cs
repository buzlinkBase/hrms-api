using Asp.Versioning;
using Hrms.Core.Messaging.LeaveWorkers;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class LeavesController : ControllerBase
    {
        private readonly LeaveService _service;
        private readonly LeaveLedgerService _leaveLedgerService;
        private readonly IMapper _mapper;

        public LeavesController(LeaveService service, LeaveLedgerService leaveLedgerService, IMapper mapper)
        {
            _service = service;
            _leaveLedgerService = leaveLedgerService;
            _mapper  = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<LeaveModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<LeaveModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<LeaveModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<LeaveModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<LeaveModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateLeave payload, CancellationToken token)
        {
            var data = _mapper.Map<Leave>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<LeaveModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<LeaveModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateLeave payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<LeaveModel>(payload));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }

        /// <summary>
        /// Manually trigger the leave period grant for a given year.
        /// Creates LeaveCredits and Grant ledger entries for all eligible employees.
        /// Defaults to the current year if no year is provided.
        /// </summary>
        [HttpPost("credits/grant")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> TriggerPeriodGrant(
            [FromServices] IPublishEndpoint publisher,
            [FromQuery] int? year,
            CancellationToken token)
        {
            var targetYear = year ?? DateTime.UtcNow.Year;
            await publisher.Publish(new RunLeavePeriodGrant(targetYear), token);
            return Ok($"Leave period grant triggered for {targetYear}. Credits will be created shortly.");
        }

        /// <summary>
        /// Current balance for one employee/leave/year — feeds the Leave Balance Entry form
        /// so HR sees what's already on record before adjusting it. Returns a default
        /// all-zero balance (200, not 404) when nothing has been granted/adjusted yet for
        /// this combination, matching this app's "no results is still a valid result"
        /// convention for lookups.
        /// </summary>
        [HttpGet("credits")]
        [ProducesResponseType(typeof(ResponseModel<LeaveCreditsBalanceModel>), 200)]
        public async Task<IActionResult> GetCreditsBalance(
            [FromQuery] Guid employeeId, [FromQuery] Guid leaveId, [FromQuery] int year, CancellationToken token)
        {
            var balance = await _leaveLedgerService.GetBalanceAsync(employeeId, leaveId, year, token);
            return Ok(balance ?? new LeaveCreditsBalanceModel
            {
                EmployeeId = employeeId,
                LeaveId = leaveId,
                PeriodYear = year,
            });
        }

        /// <summary>
        /// Manually correct one employee's leave credits balance for a given leave type and
        /// year. Records a LedgerEntryType.Adjustment entry rather than overwriting the
        /// balance directly, so the ledger's audit trail stays intact. Creates the
        /// LeaveCredits row if one doesn't exist yet for that employee/leave/year.
        /// </summary>
        [HttpPost("credits/adjust")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> AdjustCredits([FromBody] AdjustLeaveCreditsPayload payload, CancellationToken token)
        {
            var credits = await _leaveLedgerService.AdjustBalanceAsync(payload, token);
            return Ok(new { credits.Id, credits.Balance, credits.Granted, credits.Used });
        }
    }
}
