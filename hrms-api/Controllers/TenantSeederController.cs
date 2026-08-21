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
    public class TenantSeederController  : ControllerBase
    {
        private readonly AccountInitService _service;

        public TenantSeederController(AccountInitService accountInit)
        {
            _service = accountInit;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Seed( CancellationToken token)
        {
            await _service.Create(token);
            return Ok();
        }
    }
}
