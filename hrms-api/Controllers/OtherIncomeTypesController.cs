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
    public class OtherIncomeTypesController : ControllerBase
    {
        private readonly OtherIncomeTypeService _service;
        private readonly IMapper _mapper;
        public OtherIncomeTypesController(OtherIncomeTypeService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<OtherIncomeTypeModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            var result = _mapper.Map<List<OtherIncomeTypeModel>>(data);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<OtherIncomeTypeModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            var result = _mapper.Map<OtherIncomeTypeModel>(data);
            return Ok(result);
        }

        [HttpPost]
        [RequirePermission("Deductions & Income Setup:Create")]
        [ProducesResponseType(typeof(ResponseModel<OtherIncomeTypeModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateOtherIncomeType payload, CancellationToken token)
        {
            var data = _mapper.Map<OtherIncomeType>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<OtherIncomeTypeModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [RequirePermission("Deductions & Income Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<OtherIncomeTypeModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateOtherIncomeType payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<OtherIncomeTypeModel>(payload));
        }

        [HttpDelete("{id}")]
        [RequirePermission("Deductions & Income Setup:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
