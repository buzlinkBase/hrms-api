using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class DeductionApplicationsApplicationsController : ControllerBase
    {
        private readonly DeductionApplicationService _service;
        private readonly IMapper _mapper;
        public DeductionApplicationsApplicationsController(DeductionApplicationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [RequirePermission("Loan/Deduction:View")]
        [ProducesResponseType(typeof(ResponseModel<List<DeductionApplicationModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            var result = _mapper.Map<List<DeductionApplicationModel>>(data);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [RequirePermission("Loan/Deduction:View")]
        [ProducesResponseType(typeof(ResponseModel<DeductionApplicationModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            var result = _mapper.Map<DeductionApplicationModel>(data);
            return Ok(result);
        }

        // Admin-created loans are auto-approved on creation (no separate approve step for
        // admin-originated ones) -- still a Create action, the auto-approve is a business-logic
        // detail.
        [HttpPost]
        [RequirePermission("Loan/Deduction:Create")]
        [ProducesResponseType(typeof(ResponseModel<DeductionApplicationModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateDeductionApplication payload, CancellationToken token)
        {
            var data = await _service.AddAsync(payload, ApprovalStatus.Approved, token);
            var respModel = _mapper.Map<DeductionApplicationModel>(data);
            return Ok(respModel);
        }

        // Clean edit -- Approve/Decline are dedicated endpoints below, no any-of needed here.
        [HttpPut("{id}")]
        [RequirePermission("Loan/Deduction:Edit")]
        [ProducesResponseType(typeof(ResponseModel<DeductionApplicationModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateDeductionApplication payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            var data = await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<DeductionApplicationModel>(data));
        }

        [HttpDelete("{id}")]
        [RequirePermission("Loan/Deduction:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteParent(id, token);
            return Ok();
        }

        [HttpDelete("item/{id}")]
        [RequirePermission("Loan/Deduction:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> DeleteItem(Guid id, CancellationToken token)
        {
            await _service.DeleteChildAsync(id, token);
            return Ok();
        }

        [HttpPut("{id}/approve")]
        [RequirePermission("Loan/Deduction:Approve")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Approve(Guid id, CancellationToken token)
        {
            await _service.ApproveAsync(id, token);
            return Ok();
        }

        [HttpPut("{id}/decline")]
        [RequirePermission("Loan/Deduction:Approve")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Decline(Guid id, CancellationToken token)
        {
            await _service.DeclineAsync(id, token);
            return Ok();
        }
    }
}
