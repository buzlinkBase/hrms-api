using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Api.RateLimiting;
using Hrms.Core.Services.Attachments;
using Hrms.Domain.Entities.Attachments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hrms.Api.Controllers;

/// <summary>
/// Files attached to records (leave applications, employee documents, assets, ...). What each
/// caller may do depends on the record's feature -- see <see cref="IAttachmentOwnerHandler"/> --
/// and a company must switch the feature on before it accepts new files.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class AttachmentsController : ControllerBase
{
    private readonly AttachmentService _service;
    private readonly EmployeeService _employeeService;

    public AttachmentsController(AttachmentService service, EmployeeService employeeService)
    {
        _service = service;
        _employeeService = employeeService;
    }

    private async Task<AttachmentCaller> CallerAsync(CancellationToken token)
    {
        var employeeId = await _employeeService.ResolveEmployeeIdAsync(
            User.GetRequiredUserId(), User.GetUserClaim("email"), token);
        return User.ToAttachmentCaller(employeeId);
    }

    /// <summary>Which features accept attachments. Any signed-in user may read it, so forms know whether to show an upload.</summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(ResponseModel<List<AttachmentSettingDto>>), 200)]
    public async Task<IActionResult> GetSettings(CancellationToken token) =>
        Ok(await _service.GetSettingsAsync(token));

    [HttpPut("settings")]
    [RequirePermission("Organization Setup:Edit")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> SaveSettings([FromBody] List<AttachmentSettingDto> payload, CancellationToken token)
    {
        await _service.SaveSettingsAsync(payload, token);
        return Ok();
    }

    /// <summary>
    /// Uploads one file (PNG, JPG, WebP or PDF, up to 5 MB). With <c>ownerId</c> it is attached to
    /// that record; without, it is staged for a record that isn't saved yet.
    /// </summary>
    [HttpPost("{ownerType}")]
    [EnableRateLimiting(RateLimitPolicies.FileUpload)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ResponseModel<AttachmentDto>), 200)]
    public async Task<IActionResult> Upload(
        AttachmentOwnerType ownerType, [FromQuery] Guid? ownerId, IFormFile file, CancellationToken token)
    {
        await using var upload = file.OpenReadStream();
        var dto = await _service.UploadAsync(
            ownerType, ownerId, upload, file.Length, file.FileName, await CallerAsync(token), token);
        return Ok(dto);
    }

    [HttpGet("{ownerType}/{ownerId:guid}")]
    [ProducesResponseType(typeof(ResponseModel<List<AttachmentDto>>), 200)]
    public async Task<IActionResult> List(AttachmentOwnerType ownerType, Guid ownerId, CancellationToken token) =>
        Ok(await _service.ListAsync(ownerType, ownerId, await CallerAsync(token), token));

    /// <summary>Attaches the caller's staged files to a record that was just created.</summary>
    [HttpPost("{ownerType}/{ownerId:guid}/link")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> LinkStaged(
        AttachmentOwnerType ownerType, Guid ownerId, [FromBody] LinkAttachmentsRequest payload, CancellationToken token)
    {
        await _service.LinkStagedAsync(ownerType, ownerId, payload.AttachmentIds, await CallerAsync(token), token);
        return Ok();
    }

    /// <summary>The caller's own staged files of this type -- uploaded but not yet saved with a record.</summary>
    [HttpGet("{ownerType}/staged")]
    [ProducesResponseType(typeof(ResponseModel<List<AttachmentDto>>), 200)]
    public async Task<IActionResult> Staged(AttachmentOwnerType ownerType, CancellationToken token) =>
        Ok(await _service.ListStagedAsync(ownerType, await CallerAsync(token), token));

    /// <summary>A short-lived link to the file, or <c>{ url: null }</c> when storage has none -- then use <c>download</c>.</summary>
    [HttpGet("{id:guid}/link")]
    [ProducesResponseType(typeof(ResponseModel<AttachmentLink>), 200)]
    public async Task<IActionResult> Link(Guid id, CancellationToken token)
    {
        var file = await _service.OpenAsync(id, wantContent: false, await CallerAsync(token), token);
        if (file.Content != null) await file.Content.DisposeAsync();
        return Ok(new AttachmentLink(file.Url?.ToString()));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken token)
    {
        var file = await _service.OpenAsync(id, wantContent: true, await CallerAsync(token), token);
        // FileStreamResult disposes the stream. "inline" so a PDF/image opens in the tab.
        Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Content!, file.ContentType);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token)
    {
        await _service.DeleteAsync(id, await CallerAsync(token), token);
        return Ok();
    }
}

public sealed record AttachmentLink(string? Url);

public sealed record LinkAttachmentsRequest(List<Guid> AttachmentIds);
