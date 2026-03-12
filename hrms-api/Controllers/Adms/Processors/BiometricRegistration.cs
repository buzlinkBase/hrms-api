using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers.Adms
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class BiometricRegistrations : ControllerBase
    {
        private readonly BiometricDeviceService _service;
        public BiometricRegistrations(BiometricDeviceService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateBiometricDevice payload, CancellationToken token)
        {
            var data = await _service.AddAsync(payload, token);
            return Ok(data);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateBiometricDevice payload, CancellationToken token)
        {
            var data = await _service.UpdateAsync(payload, token);
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var data = await _service.FindAllAsync(token);
            return Ok(data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var data = await _service.FineOneAsync(id, token);
            return Ok(data);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }
    }
}
