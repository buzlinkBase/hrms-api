using Asp.Versioning;
using Hrms.Core.Services.StorageManager;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Api.Controllers
{
    /// <summary>A platform operator's delete: who did it and why, recorded in the company's audit trail.</summary>
    public sealed record InternalStorageDeleteRequest(
        string? Category, int? Year, List<string>? Keys, bool ConfirmLocked, string OperatorId, string? OperatorName, string Reason);

    // Service-to-service: the Platform Console (through Tenant Service) shows and cleans up one
    // company's stored files for an account owner who asked support for help. Api-key protected,
    // no user token; the company is the route's, and Tenant Service has already checked that it is
    // one of the account's companies and that the operator may do this.
    [Route("api/v{version:apiVersion}/internal/storage-manager/{tenantId:guid}")]
    [ApiVersion("1.0")]
    [ApiController]
    [AllowAnonymous]
    [ApiKey]
    public class InternalStorageManagerController : ControllerBase
    {
        private readonly StorageManagerService _service;
        private readonly ITenantProvider _tenantProvider;

        public InternalStorageManagerController(StorageManagerService service, ITenantProvider tenantProvider)
        {
            _service = service;
            _tenantProvider = tenantProvider;
        }

        private Guid Company(Guid tenantId)
        {
            if (tenantId == Guid.Empty) throw new GuardException("No company was given.");
            // Attachment owners and the audit entry read the company from the tenant provider.
            _tenantProvider.SetTenantId(tenantId);
            return tenantId;
        }

        [HttpGet("breakdown")]
        [ProducesResponseType(typeof(ResponseModel<StorageBreakdownDto>), 200)]
        public async Task<IActionResult> Breakdown(Guid tenantId, CancellationToken token) =>
            Ok(await _service.GetBreakdownAsync(Company(tenantId), token));

        [HttpGet("files")]
        [ProducesResponseType(typeof(ResponseModel<StoredFilePage>), 200)]
        public async Task<IActionResult> Files(
            Guid tenantId, [FromQuery] string category, [FromQuery] int year, [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20, CancellationToken token = default) =>
            Ok(await _service.GetFilesAsync(Company(tenantId), category, year, page, pageSize, token));

        [HttpPost("delete/preview")]
        [ProducesResponseType(typeof(ResponseModel<StorageDeletePreview>), 200)]
        public async Task<IActionResult> PreviewDelete(Guid tenantId, [FromBody] StorageDeleteRequest request, CancellationToken token) =>
            Ok(await _service.PreviewDeleteAsync(Company(tenantId), request, token));

        [HttpPost("delete")]
        [ProducesResponseType(typeof(ResponseModel<StorageDeleteResult>), 200)]
        public async Task<IActionResult> Delete(Guid tenantId, [FromBody] InternalStorageDeleteRequest request, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(request.OperatorId)) throw new GuardException("The operator is required.");
            if (string.IsNullOrWhiteSpace(request.Reason)) throw new GuardException("A reason is required.");
            var caller = StorageManagerCaller.PlatformOperator(request.OperatorId, request.OperatorName, request.Reason.Trim());
            var delete = new StorageDeleteRequest(request.Category, request.Year, request.Keys, request.ConfirmLocked);
            return Ok(await _service.DeleteAsync(Company(tenantId), delete, caller, token));
        }
    }
}
