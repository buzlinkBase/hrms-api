using Asp.Versioning;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class EmployeeEducationsController : ControllerBase
    {
        private readonly EmployeeEducationService _service;
        private readonly IMapper _mapper;

        public EmployeeEducationsController(EmployeeEducationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }


        [HttpGet("employee")]
        public async Task<IActionResult> GetAll([FromQuery] Guid emp_id, CancellationToken token)
        {
            var data = await _service.FindAllAsync(emp_id, token);
            return Ok(_mapper.Map<List<EducationModel>>(data));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<EducationModel>(data));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateEducation payload, CancellationToken token)
        {
            var data = _mapper.Map<Education>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<EducationModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateEducation payload, CancellationToken token)
        {
            var data = _mapper.Map<Education>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<EducationModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
