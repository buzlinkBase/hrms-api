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
    public class TemplateController : ControllerBase
    {
        private readonly DeviceService _service;
        public TemplateController(DeviceService service)
        {
            _service = service;
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }
    }
}
