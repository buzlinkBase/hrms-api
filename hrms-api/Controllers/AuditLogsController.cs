using Onepunch.Common.Lib.Subscriptions;
using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Core.Services.Auditing;
using Microsoft.AspNetCore.Mvc;
using Onepunch.AuditEngine;

namespace Hrms.Api.Controllers
{
    // Security > Audit: the current company's trail -- its own hrms-api events plus the copies of
    // AuthApi/TenantApi events AuditReplicaWorker keeps. Always scoped to the caller's company.
    [Route("api/v{version:apiVersion}/audit-logs")]
    [ApiVersion("1.0")]
    [ApiController]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
    [ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
    [RequireFeature(EntitlementKeys.AuditTrail)]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditQueryService _audits;
        private readonly ITenantProvider _tenantProvider;

        public AuditLogsController(IAuditQueryService audits, ITenantProvider tenantProvider)
        {
            _audits = audits;
            _tenantProvider = tenantProvider;
        }

        [HttpGet]
        [RequirePermission("Audit Trail:View")]
        [ProducesResponseType(typeof(ResponseModel<AuditLogPage>), 200)]
        public async Task<IActionResult> Get([FromQuery] AuditLogQuery query, CancellationToken token)
        {
            var page = await _audits.QueryAsync(AuditLogPresenter.ToEngineQuery(query, CompanyId), token);
            return Ok(AuditLogPresenter.ToPage(page));
        }

        [HttpGet("modules")]
        [RequirePermission("Audit Trail:View")]
        [ProducesResponseType(typeof(ResponseModel<IReadOnlyList<AuditModuleOption>>), 200)]
        public IActionResult GetModules() => Ok(AuditLogPresenter.Modules);

        [HttpGet("{id:guid}")]
        [RequirePermission("Audit Trail:View")]
        [ProducesResponseType(typeof(ResponseModel<AuditLogResponse>), 200)]
        public async Task<IActionResult> Get(Guid id, CancellationToken token)
        {
            var auditEvent = await _audits.GetAsync(id, CompanyId, token);
            return auditEvent == null ? NotFound() : Ok(AuditLogPresenter.ToResponse(auditEvent));
        }

        private string CompanyId => _tenantProvider.TenantId.ToString();
    }
}
