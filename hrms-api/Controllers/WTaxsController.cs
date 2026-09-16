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
    public class WTaxsController : ControllerBase
    {
        private readonly TaxService _service;
        private readonly IMapper _mapper;

        public WTaxsController(TaxService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet()]
        [ProducesResponseType(typeof(ResponseModel<List<WTaxModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] string payrollType, CancellationToken token)
        {
            var data = await _service.FindAllAsync(payrollType, token);
            return Ok(_mapper.Map<List<WTaxModel>>(data));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ResponseModel<WTaxModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<WTaxModel>(data));
        }

        [HttpPost]
        [RequirePermission("Statutory Tables:Create")]
        [ProducesResponseType(typeof(ResponseModel<WTaxModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateWTax payload, CancellationToken token)
        {
            var data = _mapper.Map<TaxTable>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<WTaxModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [RequirePermission("Statutory Tables:Edit")]
        [ProducesResponseType(typeof(ResponseModel<WTaxModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateWax payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<WTaxModel>(payload));
        }

        [HttpDelete("{id}")]
        [RequirePermission("Statutory Tables:Delete")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
