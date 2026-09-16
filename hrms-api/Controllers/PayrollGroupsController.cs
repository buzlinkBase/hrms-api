using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class PayrollGroupsController : ControllerBase
    {
        private readonly PayrollGroupService _service;
        private readonly IMapper _mapper;

        public PayrollGroupsController(PayrollGroupService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<PayrollGroupModel>>), 200)]
        public async Task<IActionResult> Get(
        [FromQuery] PayrollGroupQuery query, CancellationToken token)
        {
            var data = await _service.FindAllAsync(token, query.Status);
            var result = _mapper.Map<List<PayrollGroupModel>>(data);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<PayrollGroupModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<PayrollGroupModel>(data));
        }

        [HttpPost]
        [RequirePermission("Organization Setup:Create")]
        [ProducesResponseType(typeof(ResponseModel<PayrollGroupModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreatePayrollGroup payload, CancellationToken token)
        {
            if (payload == null) return BadRequest("Invalid payload");
            var data = _mapper.Map<PayrollGroup>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<PayrollGroupModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [RequirePermission("Organization Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<PayrollGroupModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdatePayrollGroup payload, CancellationToken token)
        {
            if (payload == null) return BadRequest("Invalid payload");
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<PayrollGroupModel>(payload));
        }

        [HttpDelete("{id}")]
        [RequirePermission("Organization Setup:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
