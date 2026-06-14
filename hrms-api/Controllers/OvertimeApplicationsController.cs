using Asp.Versioning;
using Elastic.Clients.Elasticsearch.Core.TermVectors;
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
    public class OvertimeApplicationsController : ControllerBase
    {
        private readonly OvertimeApplicationService _service;
        private readonly IMapper _mapper;

        public OvertimeApplicationsController(OvertimeApplicationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<OvertimeApplicationModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<OvertimeApplicationModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<OvertimeApplicationModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<OvertimeApplicationModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<OvertimeApplicationModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateOverTimeApplication payload, CancellationToken token)
        {
            var data = _mapper.Map<OverTimeApplication>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<OvertimeApplicationModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<OvertimeApplicationModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateOvertimeApplication payload, CancellationToken token)
        {
            var data = _mapper.Map<OverTimeApplication>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<OvertimeApplicationModel>(data));
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
