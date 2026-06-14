using Asp.Versioning;
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
            var data = await _service.FindAllAsync(query.Status);
            return Ok(_mapper.Map<List<PayrollGroupModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<PayrollGroupModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<PayrollGroupModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<PayrollGroupModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreatePayrollGroup payload, CancellationToken token)
        {
            var data = _mapper.Map<PayrollGroup>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<PayrollGroupModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<PayrollGroupModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdatePayrollGroup payload, CancellationToken token)
        {
            var data = _mapper.Map<PayrollGroup>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<PayrollGroupModel>(data));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
