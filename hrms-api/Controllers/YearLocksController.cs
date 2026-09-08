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
    public class YearLocksController : ControllerBase
    {
        private readonly YearLockService _service;

        public YearLocksController(YearLockService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> GetAll(CancellationToken token)
        {
            var data = await _service.GetAllAsync(token);
            return Ok(data);
        }

        [HttpPost("{year}/reopen")]
        [ProducesResponseType(typeof(ResponseModel<object>), 200)]
        public async Task<IActionResult> Reopen(int year, CancellationToken token)
        {
            await _service.ReopenYearAsync(year, token);
            return Ok();
        }
    }
}
