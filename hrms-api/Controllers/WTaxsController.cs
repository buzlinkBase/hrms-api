using Asp.Versioning;
using AutoMapper;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class WTaxsController : ControllerBase
    {
        private readonly TaxService _service;
        private readonly IMapper _mapper;

        public WTaxsController(TaxService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }


        [HttpGet()]
        public async Task<IActionResult> Get([FromQuery] DateOnly effectivity, CancellationToken token)
        {
            var data = await _service.FindAllAsync(effectivity, token);
            return Ok(_mapper.Map<List<WTaxModel>>(data));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(_mapper.Map<WTaxModel>(data));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateWTax payload, CancellationToken token)
        {
            var data = _mapper.Map<TaxTable>(payload);
            await _service.AddAsync(data, token);
            var respModel = _mapper.Map<WTaxModel>(data);
            return Ok(respModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateWax payload, CancellationToken token)
        {
            var data = _mapper.Map<TaxTable>(payload);
            data.Id = id;
            await _service.UpdateAsync(data, token);
            return Ok(_mapper.Map<WTaxModel>(data));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.Delete(id, token);
            return Ok();
        }
    }
}
