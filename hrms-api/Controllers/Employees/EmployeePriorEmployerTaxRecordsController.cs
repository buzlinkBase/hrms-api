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
    public class EmployeePriorEmployerTaxRecordsController : ControllerBase
    {
        private readonly EmployeePriorEmployerTaxRecordService _service;
        private readonly IMapper _mapper;

        public EmployeePriorEmployerTaxRecordsController(EmployeePriorEmployerTaxRecordService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet("employee")]
        [ProducesResponseType(typeof(ResponseModel<List<PriorEmployerTaxRecordModel>>), 200)]
        public async Task<IActionResult> GetAll([FromQuery] Guid emp_id, CancellationToken token)
        {
            var data = await _service.FindAllAsync(emp_id, token);
            return Ok(_mapper.Map<List<PriorEmployerTaxRecordModel>>(data));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<PriorEmployerTaxRecordModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<PriorEmployerTaxRecordModel>(data));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<PriorEmployerTaxRecordModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreatePriorEmployerTaxRecord payload, CancellationToken token)
        {
            var data = _mapper.Map<PriorEmployerTaxRecord>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<PriorEmployerTaxRecordModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<PriorEmployerTaxRecordModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdatePriorEmployerTaxRecord payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<PriorEmployerTaxRecordModel>(payload));
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
