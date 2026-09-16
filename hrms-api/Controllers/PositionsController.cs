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
    public class PositionsController : ControllerBase
    {
        private readonly PositionService _service;
        private readonly IMapper _mapper;

        public PositionsController(PositionService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<PositionModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<PositionModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<PositionModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<PositionModel>(data));
        }

        [HttpPost]
        [RequirePermission("Organization Setup:Create")]
        [ProducesResponseType(typeof(ResponseModel<PositionModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreatePosition payload, CancellationToken token)
        {
            var data = _mapper.Map<Position>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<PositionModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [RequirePermission("Organization Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<PositionModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdatePosition payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<PositionModel>(payload));
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
