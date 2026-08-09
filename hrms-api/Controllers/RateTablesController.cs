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
    public class RateTablesController : ControllerBase
    {
        private readonly RateTableService _service;
        private readonly IMapper _mapper;

        public RateTablesController(RateTableService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<RateTableModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<RateTableModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<RateTableModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<RateTableModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<RateTableModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateRateTable payload, CancellationToken token)
        {
            var data = _mapper.Map<RateTable>(payload);
            data.Description = payload.Type.ToString();
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<RateTableModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<RateTableModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateRateTable payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<RateTableModel>(payload));
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
