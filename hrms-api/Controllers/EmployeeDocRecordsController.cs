using Asp.Versioning;
using AutoMapper;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class EmployeeDocRecordsController : ControllerBase
    {
        private readonly EmployeeRecordService _service;
        private readonly IMapper _mapper;

        public EmployeeDocRecordsController(EmployeeRecordService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }


        [HttpGet("employee")]
        public async Task<IActionResult> GetAll([FromQuery] Guid emp_id, CancellationToken token)
        {
            var data = await _service.FindAllAsync(emp_id, token);
            return Ok(_mapper.Map<List<EmployeeRecordModel>>(data));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<EmployeeRecordModel>(data));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateEmployeeRecord payload, CancellationToken token)
        {
            var data = _mapper.Map<EmployeeRecord>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<EmployeeRecordModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateEmployeeRecord payload, CancellationToken token)
        {
            var data = _mapper.Map<EmployeeRecord>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<EmployeeRecordModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
