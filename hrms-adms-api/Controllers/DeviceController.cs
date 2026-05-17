using Asp.Versioning;
using Hrms.adms.Core.Services;
using Hrms.adms.Extensions;
using Hrms.adms.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.adms.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]
    public class DeviceController : ControllerBase
    {
        private readonly DeviceService _service;
        public DeviceController(DeviceService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateBiometricDevice payload, CancellationToken token)
        {
            var tenantId = HttpContext.User.GetUserClaim("TenantId")?.ToString() 
                ?? Guid.Empty.ToString();
            var data = await _service.AddAsync(payload, Guid.Parse(tenantId), token);
            return Ok(data);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateBiometricDevice payload, CancellationToken token)
        {
            var data = await _service.UpdateStatusAsync(payload, token);
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

        [HttpGet("serial/{sn}")]
        public async Task<IActionResult> GetBySerial(string sn, CancellationToken token)
        {
            var data = await _service.GetBySerial(sn, token);
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
