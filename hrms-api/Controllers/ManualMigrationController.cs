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
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    public class ManualMigrationController : ControllerBase
    {
        private readonly ManualMigrationService _service;
        public ManualMigrationController(ManualMigrationService service)
        {
            _service = service;
        }

        [HttpPost]
        [ProducesResponseType(204)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> Get([FromBody] MigrationPayload payload, CancellationToken token)
        {
            Guid.TryParse(User.GetUserClaim("TenantId"), out Guid tenantId);
            if (tenantId == Guid.Empty)
            {
                return Unauthorized("Invalid token");
            }
            var migrationPayload = new MigrateTenantDb
            {
                CurrentVersion = payload.CurrentVersion,
                TargetVersion = payload.TargetVersion,
                System = "HRIS",
                TenantId = tenantId
            };
            await _service.Migrate(migrationPayload);
            return NoContent();
        }
    }
}
public record MigrationPayload(string CurrentVersion, string TargetVersion);
