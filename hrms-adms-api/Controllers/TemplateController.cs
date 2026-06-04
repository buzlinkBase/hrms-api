using Asp.Versioning;
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
        private readonly TemplateService _service;
        public TemplateController(TemplateService service)
        {
            _service = service;
        }

        [HttpGet()]
        public async Task<IActionResult> GetAll(string sn, CancellationToken token)
        {
            var result = await _service.FindAll(sn, token);
            return Ok(result);
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            await _service.DeleteAsync(id, token);
            return Ok();
        }
    }
}
