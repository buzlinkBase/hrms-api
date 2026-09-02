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
    public class CostCentersController : ControllerBase
    {
        private readonly CostCenterService _service;
        private readonly IMapper _mapper;

        public CostCentersController(CostCenterService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<CostCenterModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateCostCenter payload, CancellationToken token)
        {
            var data = _mapper.Map<CostCenters>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<CostCenterModel>(data);
            return Ok(respModel);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<CostCenterModel>>), 200)]
        public async Task<IActionResult> Get([FromQuery] Guid? branchId)
        {
            var data = await _service.FindAllAsync(branchId);
            return Ok(_mapper.Map<List<CostCenterModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<CostCenterModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<CostCenterModel>(data));
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<CostCenterModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateCostCenter payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<CostCenterModel>(payload));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return NoContent();
        }
    }
}
