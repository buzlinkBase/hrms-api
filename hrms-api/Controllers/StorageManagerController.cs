using Asp.Versioning;
using Hrms.Core.Services.StorageManager;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

/// <summary>
/// Billing > Storage: what fills the current company's file storage, by category and year, and
/// deleting files that are no longer needed so the account uses the storage it already has.
/// Owner always passes; Admin has Storage:View/Manage by default; other roles need them granted.
/// </summary>
[Route("api/v{version:apiVersion}/storage-manager")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class StorageManagerController : ControllerBase
{
    public const string ViewPermission = "Storage:View";
    public const string ManagePermission = "Storage:Manage";

    private readonly StorageManagerService _service;
    private readonly ITenantProvider _tenantProvider;

    public StorageManagerController(StorageManagerService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("breakdown")]
    [RequirePermission(ViewPermission, ManagePermission)]
    [ProducesResponseType(typeof(ResponseModel<StorageBreakdownDto>), 200)]
    public async Task<IActionResult> Breakdown(CancellationToken token) =>
        Ok(await _service.GetBreakdownAsync(_tenantProvider.TenantId, token));

    [HttpGet("files")]
    [RequirePermission(ViewPermission, ManagePermission)]
    [ProducesResponseType(typeof(ResponseModel<StoredFilePage>), 200)]
    public async Task<IActionResult> Files(
        [FromQuery] string category, [FromQuery] int year, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken token = default) =>
        Ok(await _service.GetFilesAsync(_tenantProvider.TenantId, category, year, page, pageSize, token));

    /// <summary>How many files a delete would remove, how much it frees and how many belong to decided records.</summary>
    [HttpPost("delete/preview")]
    [RequirePermission(ManagePermission)]
    [ProducesResponseType(typeof(ResponseModel<StorageDeletePreview>), 200)]
    public async Task<IActionResult> PreviewDelete([FromBody] StorageDeleteRequest request, CancellationToken token) =>
        Ok(await _service.PreviewDeleteAsync(_tenantProvider.TenantId, request, token));

    [HttpPost("delete")]
    [RequirePermission(ManagePermission)]
    [ProducesResponseType(typeof(ResponseModel<StorageDeleteResult>), 200)]
    public async Task<IActionResult> Delete([FromBody] StorageDeleteRequest request, CancellationToken token) =>
        Ok(await _service.DeleteAsync(_tenantProvider.TenantId, request, StorageManagerCaller.SignedInUser, token));
}
