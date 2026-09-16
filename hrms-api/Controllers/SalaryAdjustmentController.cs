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
    public class SalaryAdjustmentmentController : ControllerBase
    {
        private readonly SalaryAdjustmentService _service;
        private readonly IMapper _mapper;
        public SalaryAdjustmentmentController(SalaryAdjustmentService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [RequirePermission("Salary Adjustment:View")]
        [ProducesResponseType(typeof(ResponseModel<List<SalaryAdjustmentModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<SalaryAdjustmentModel>>(data));
        }

        [HttpGet("{id}")]
        [RequirePermission("Salary Adjustment:View")]
        [ProducesResponseType(typeof(ResponseModel<SalaryAdjustmentModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<SalaryAdjustmentModel>(data));
        }

        [HttpPost]
        [RequirePermission("Salary Adjustment:Create")]
        [ProducesResponseType(typeof(ResponseModel<SalaryAdjustmentModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateSalaryAdjustment payload, CancellationToken token)
        {
            var data = _mapper.Map<SalaryAdjustment>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<SalaryAdjustmentModel>(data);
            return Ok(respModel);
        }

        // No approval workflow exists for Salary Adjustment at all -- plain :Edit.
        [HttpPut("{id}")]
        [RequirePermission("Salary Adjustment:Edit")]
        [ProducesResponseType(typeof(ResponseModel<SalaryAdjustmentModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateSalaryAdjustment payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<SalaryAdjustmentModel>(payload));
        }

        [HttpDelete("{id}")]
        [RequirePermission("Salary Adjustment:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
