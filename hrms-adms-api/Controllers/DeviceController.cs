using Asp.Versioning;
using Hrms.adms.Extensions;
using Hrms.adms.Models.DTO;
using Hrms.adms.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.adms.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    public class DeviceController : ControllerBase
    {
        private readonly DeviceService _service;
        public DeviceController(DeviceService service)
        {
            _service = service;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<UpdateBiometricDevice>), 200)]
        public async Task<IActionResult> Post([FromBody] CreateBiometricDevice payload, CancellationToken token)
        {
            var tenantId = HttpContext.ParseTenant();
            if (tenantId == Guid.Empty) return Forbid("cannot parse tenant");
            var data = await _service.AddAsync(payload, tenantId, token);
            return Ok(data);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ResponseModel<UpdateBiometricDevice>), 200)]
        public async Task<IActionResult> Put(Guid id, [FromBody] UpdateBiometricDevice payload, CancellationToken token)
        {
            var tenantId = HttpContext.ParseTenant();
            if (tenantId == Guid.Empty) return Forbid("cannot parse tenant");
            var data = await _service.UpdateStatusAsync(payload, tenantId, token);
            return Ok(data);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<BiometricDeviceModel>>), 200)]
        public async Task<IActionResult> Get(CancellationToken token)
        {
            var tenantId = HttpContext.ParseTenant();
            if (tenantId == Guid.Empty) return Forbid("cannot parse tenant");
            var data = await _service.FindAllAsync(tenantId, token);
            return Ok(data);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ResponseModel<BiometricDeviceModel>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var tenantId = HttpContext.ParseTenant();
            if (tenantId == Guid.Empty) return Forbid("cannot parse tenant");
            var data = await _service.FineOneAsync(id, tenantId, token);
            return Ok(data);
        }

        [HttpGet("serial/{sn}")]
        [ProducesResponseType(typeof(ResponseModel<BiometricDeviceModel>), 200)]
        public async Task<IActionResult> GetBySerial(string sn, CancellationToken token)
        {
            var tenantId = HttpContext.ParseTenant();
            if (tenantId == Guid.Empty) return Forbid("cannot parse tenant");

            var data = await _service.GetBySerial(sn, tenantId, token);
            return Ok(data);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }
    }
}
