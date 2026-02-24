using Asp.Versioning;
using AutoMapper;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class SalaryAdjustmentmentController : ControllerBase
    {
        private readonly SalaryAdjustmentService _service;
        private readonly IMapper _mapper;
        public SalaryAdjustmentmentController(SalaryAdjustmentService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(_mapper.Map<List<SalaryAdjustmentModel>>(data));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<SalaryAdjustmentModel>(data));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateSalaryAdjustment payload, CancellationToken token)
        {
            var data = _mapper.Map<SalaryAdjustment>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<SalaryAdjustmentModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateSalaryAdjustment payload, CancellationToken token)
        {
            var data = _mapper.Map<SalaryAdjustment>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<SalaryAdjustmentModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
