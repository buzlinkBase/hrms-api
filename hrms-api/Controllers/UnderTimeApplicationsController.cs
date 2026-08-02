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
    public class UnderTimeApplicationsController : ControllerBase
    {
        private readonly UnderTimeApplicationService _service;
        private readonly IMapper _mapper;

        public UnderTimeApplicationsController(UnderTimeApplicationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<UnderTimeApplicationModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<UnderTimeApplicationModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<UnderTimeApplicationModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<UnderTimeApplicationModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<UnderTimeApplicationModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateUnderTimeApplication payload, CancellationToken token)
        {
            var data = _mapper.Map<UnderTimeApplication>(payload);
            await _service.AddAsync(data, token);
            return Ok(_mapper.Map<UnderTimeApplicationModel>(data));
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<UnderTimeApplicationModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateUnderTimeApplication payload, CancellationToken token)
        {
            var data = _mapper.Map<UnderTimeApplication>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<UnderTimeApplicationModel>(data));
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
