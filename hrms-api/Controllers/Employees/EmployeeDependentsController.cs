using Asp.Versioning;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class EmployeeDependentsController : ControllerBase
    {
        private readonly EmployeeDependentService _service;
        private readonly IMapper _mapper;

        public EmployeeDependentsController(EmployeeDependentService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet("employee")]
        [ProducesResponseType(typeof(ResponseModel<List<DependentModel>>), 200)]
        public async Task<IActionResult> GetAll([FromQuery] Guid emp_id, CancellationToken token)
        {
            var data = await _service.FindAllAsync(emp_id, token);
            return Ok(_mapper.Map<List<DependentModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<DependentModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<DependentModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<DependentModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateDependent payload, CancellationToken token)
        {
            var data = _mapper.Map<Dependent>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<DependentModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<DependentModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateDependent payload, CancellationToken token)
        {
            var data = _mapper.Map<Dependent>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<DependentModel>(data));
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
