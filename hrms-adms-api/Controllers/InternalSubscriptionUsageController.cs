using Asp.Versioning;
using Hrms.adms.Filters;
using Hrms.adms.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.adms.Controllers
{
    // Service-to-service: Tenant Service reads each company's active device count here when it
    // checks a plan downgrade. Api-key protected, no user token.
    [Route("api/v{version:apiVersion}/internal/subscription-usage")]
    [ApiVersion("1.0")]
    [ApiController]
    [AllowAnonymous]
    [ApiKey]
    public class InternalSubscriptionUsageController : ControllerBase
    {
        private const int MaxCompaniesPerCall = 100;

        private readonly DeviceAllowanceService _devices;

        public InternalSubscriptionUsageController(DeviceAllowanceService devices)
        {
            _devices = devices;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<CompanyUsage>>), 200)]
        public async Task<IActionResult> Get([FromQuery] List<Guid> tenantIds, CancellationToken token)
        {
            var ids = tenantIds.Where(id => id != Guid.Empty).Distinct().Take(MaxCompaniesPerCall).ToList();
            var counts = await _devices.CountActiveAsync(ids, token);
            return Ok(ids.Select(id => new CompanyUsage
            {
                TenantId = id,
                Used = new() { [EntitlementKeys.BiometricDevicesMax] = counts.GetValueOrDefault(id) },
            }).ToList());
        }
    }
}
