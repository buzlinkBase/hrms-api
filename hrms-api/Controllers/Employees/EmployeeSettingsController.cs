using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Core.Services.Compliance;
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
        private readonly EmployeePayDeviationService _payDeviations;

        public EmployeeSettingsController(EmployeeSettingService service, IMapper mapper, EmployeePayDeviationService payDeviations)
        {
            _service = service;
            _mapper = mapper;
            _payDeviations = payDeviations;
        }

        // Excluding an employee from a mandatory benefit is logged (who and when).
        private Task RecordDeviationsAsync(CreateEmployeeSetting settings, CancellationToken token) =>
            _payDeviations.RecordDeviationsAsync(
                [new EmployeePayCheck(null, null, 0, Eligibility: EmployeePayCheck.From(settings))],
                HttpContext.ToComplianceActor(), token);

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<CreateEmployeeSetting>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<CreateEmployeeSetting>(data));
        }

        [HttpPost]
        [RequirePermission("Workforce Setup:Create")]
        [ProducesResponseType(typeof(ResponseModel<CreateEmployeeSetting>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateEmployeeSetting payload, CancellationToken token)
        {
            await RecordDeviationsAsync(payload, token);
            var data = _mapper.Map<EmployeeSetting>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<CreateEmployeeSetting>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [RequirePermission("Workforce Setup:Edit")]
        [ProducesResponseType(typeof(ResponseModel<EmployeeSettingModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateEmployeeSetting payload, CancellationToken token)
        {
            await RecordDeviationsAsync(payload, token);
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<EmployeeSettingModel>(payload));
        }
    }
}
