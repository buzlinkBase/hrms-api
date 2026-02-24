using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class DeductionApplicationsApplicationsController : ControllerBase
    {
        private readonly DeductionApplicationService _service;
        private readonly IMapper _mapper;
        public DeductionApplicationsApplicationsController(DeductionApplicationService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            var result = _mapper.Map<List<DeductionApplicationModel>>(data);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            var result = _mapper.Map<DeductionApplicationModel>(data);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateDeductionApplication payload, CancellationToken token)
        {
            var data = await _service.AddAsync(payload, token);
            var respModel = _mapper.Map<DeductionApplicationModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateDeductionApplication payload, CancellationToken token)
        {
            var data = await _service.UpdateAsync(payload, token);
            return Ok(_mapper.Map<DeductionApplicationModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteParent(id, token);
            return Ok();
        }
        [HttpDelete("item/{id}")]
        public async Task<IActionResult> DeleteItem(Guid id, CancellationToken token)
        {
            await _service.DeleteChildAsync(id, token);
            return Ok();
        }
    }
}
