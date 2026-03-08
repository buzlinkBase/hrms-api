using Asp.Versioning;
using AutoMapper;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class EmploymentHistoriesController : ControllerBase
    {
        private readonly EmploymentHistoryService _service;
        private readonly IMapper _mapper;

        public EmploymentHistoriesController(EmploymentHistoryService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }


        [HttpGet("employee")]
        public async Task<IActionResult> GetAll([FromQuery] Guid emp_id, CancellationToken token)
        {
            var data = await _service.FindAllAsync(emp_id, token);
            return Ok(_mapper.Map<List<EmploymentHistoryModel>>(data));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<EmploymentHistoryModel>(data));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateEmploymentHistory payload, CancellationToken token)
        {
            var data = _mapper.Map<EmploymentHistory>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<EmploymentHistoryModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateEmploymentHistory payload, CancellationToken token)
        {
            var data = _mapper.Map<EmploymentHistory>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<EmploymentHistoryModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
