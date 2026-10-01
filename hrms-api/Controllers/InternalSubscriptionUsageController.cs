using Asp.Versioning;
using Hrms.Core.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.Api.Controllers
{
    // Service-to-service: Tenant Service reads each company's employee count and feature usage
    // here when it checks a plan downgrade or recommends a plan. Api-key protected, no user token.
    [Route("api/v{version:apiVersion}/internal/subscription-usage")]
    [ApiVersion("1.0")]
    [ApiController]
    [AllowAnonymous]
    [ApiKey]
    public class InternalSubscriptionUsageController : ControllerBase
    {
        private const int MaxCompaniesPerCall = 100;

        private readonly SubscriptionUsageService _usage;

        public InternalSubscriptionUsageController(SubscriptionUsageService usage)
        {
            _usage = usage;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel<List<CompanyUsage>>), 200)]
        public async Task<IActionResult> Get([FromQuery] List<Guid> tenantIds, CancellationToken token)
        {
            var ids = tenantIds.Where(id => id != Guid.Empty).Distinct().Take(MaxCompaniesPerCall).ToList();
            return Ok(await _usage.GetUsageAsync(ids, token));
        }
    }
}
