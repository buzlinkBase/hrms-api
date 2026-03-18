using Asp.Versioning;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class LeavesController : ControllerBase
    {
        private readonly LeaveService _service;
        private readonly IMapper _mapper;

        public LeavesController(LeaveService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }


        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<LeaveModel>>(data));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<LeaveModel>(data));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateLeave payload, CancellationToken token)
        {
            var data = _mapper.Map<Leave>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<LeaveModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateLeave payload, CancellationToken token)
        {
            var data = _mapper.Map<Leave>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<LeaveModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }
    }
}
