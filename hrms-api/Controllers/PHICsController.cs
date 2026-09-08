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
    public class PHICsController : ControllerBase
    {
        private readonly PHICService _service;
        private readonly IMapper _mapper;

        public PHICsController(PHICService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<PHICModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<PHICModel>>(data));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ResponseModel<PHICModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<PHICModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<PHICModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreatePHIC payload, CancellationToken token)
        {
            var data = _mapper.Map<PHICTable>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<PHICModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<PHICModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdatePHIC payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<PHICModel>(payload));
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
