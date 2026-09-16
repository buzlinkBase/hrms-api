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
    public class AnnualTaxsController : ControllerBase
    {
        private readonly AnnualTaxService _service;
        private readonly IMapper _mapper;

        public AnnualTaxsController(AnnualTaxService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<AnnualTaxModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<AnnualTaxModel>>(data));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ResponseModel<AnnualTaxModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<AnnualTaxModel>(data));
        }

        [HttpPost]
        [RequirePermission("Statutory Tables:Create")]
        [ProducesResponseType(typeof(ResponseModel<AnnualTaxModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateAnnualTax payload, CancellationToken token)
        {
            var data = _mapper.Map<AnnualTaxTable>(payload);
            await _service.AddAsync(data, token);
            return Ok(_mapper.Map<AnnualTaxModel>(data));
        }

        [HttpPut("{id}")]
        [RequirePermission("Statutory Tables:Edit")]
        [ProducesResponseType(typeof(ResponseModel<AnnualTaxModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateAnnualTax payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<AnnualTaxModel>(payload));
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
