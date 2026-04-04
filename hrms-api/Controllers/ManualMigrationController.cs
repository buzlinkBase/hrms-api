using Asp.Versioning;
using Hrms.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]
    public class ManualMigrationController : ControllerBase
    {
        private readonly ManualMigrationService _service;
        public ManualMigrationController(ManualMigrationService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Get([FromBody] MigrationPayload payload, CancellationToken token)
        {
            var tenantId = User.GetRequiredUserId();
            await _service.Migrate(new MigrateTenantDb
            {
                CurrentVersion = "",
                TargetVersion = "",
                System = "HRIS",
                TenantId = tenantId
            });
            return Ok();
        }
    }
}
public record MigrationPayload(string CurrentVersion, string TargetVersion);