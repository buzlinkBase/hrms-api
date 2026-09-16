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
    public class OtherIncomesController : ControllerBase
    {
        private readonly OtherIncomeService _service;
        private readonly IMapper _mapper;
        public OtherIncomesController(OtherIncomeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<OtherIncomeModel>>), 200)]
        public async Task<IActionResult> Get()
        {
            var data = await _service.FindAllAsync();
            var result = _mapper.Map<List<OtherIncomeModel>>(data);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<OtherIncomeModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            var result = _mapper.Map<OtherIncomeModel>(data);
            return Ok(result);
        }

        [HttpPost]
        [RequirePermission("Deductions & Income Setup:Create")]
        [ProducesResponseType(typeof(ResponseModel<OtherIncomeModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateOtherIncome payload, CancellationToken token)
        {
            var data = _mapper.Map<OtherIncome>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<OtherIncomeModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [RequirePermission("Deductions & Income Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<OtherIncomeModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateOtherIncome payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<OtherIncomeModel>(payload));
        }

        [HttpDelete("{id}")]
        [RequirePermission("Deductions & Income Setup:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }
    }
}
