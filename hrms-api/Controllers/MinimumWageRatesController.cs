using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class MinimumWageRatesController : ControllerBase
    {
        private readonly MinimumWageRateService _service;

        public MinimumWageRatesController(MinimumWageRateService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<MinimumWageRateModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(data);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<MinimumWageRateModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(data);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<MinimumWageRateModel>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateMinimumWageRate payload, CancellationToken token)
        {
            var response = await _service.AddAsync(payload, token);
            return Ok(response);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<MinimumWageRateModel>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateMinimumWageRate payload, CancellationToken token)
        {
            payload.Id = payload.Id == Guid.Empty ? id : payload.Id;
            var response = await _service.UpdateAsync(payload, token);
            return Ok(response);
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
