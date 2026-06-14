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
    public class EmployeeSettingsController : ControllerBase
    {
        private readonly EmployeeSettingService _service;
        private readonly IMapper _mapper;

        public EmployeeSettingsController(EmployeeSettingService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<CreateEmployeeSetting>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<CreateEmployeeSetting>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<CreateEmployeeSetting>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateEmployeeSetting payload, CancellationToken token)
        {
            var data = _mapper.Map<EmployeeSetting>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<CreateEmployeeSetting>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<EmployeeSettingModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateEmployeeSetting payload, CancellationToken token)
        {
            var data = _mapper.Map<EmployeeSetting>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<EmployeeSettingModel>(data));
        }
    }
}
