using Asp.Versioning;
using Hrms.Api.Filters;
using Hrms.Api.RateLimiting;
using Hrms.Core.Services;
using Hrms.Core.Services.ClientPortal;
using Hrms.Core.Services.Storage;
using Microsoft.AspNetCore.RateLimiting;
using Hrms.Domain.ValueObjects;
using Hrms.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using ApiException = Refit.ApiException;

namespace Hrms.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class CompanySettingsController : ControllerBase
{
    private readonly CompanyService _service;
    private readonly IMapper _mapper;
    private readonly ITenantClient _tenantClient;
    private readonly OrganizationImageService _images;
    private readonly ImageUrlResolver _imageUrls;
    private readonly IClientPortalBrandingPublisher? _portalBranding;

    public CompanySettingsController(CompanyService service, IMapper mapper, ITenantClient tenantClient,
        OrganizationImageService images, ImageUrlResolver imageUrls, IClientPortalBrandingPublisher? portalBranding = null)
    {
        _portalBranding = portalBranding;
        _service = service;
        _mapper = mapper;
        _tenantClient = tenantClient;
        _images = images;
        _imageUrls = imageUrls;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ResponseModel<CompanyModel>), 200)]
    public async Task<IActionResult> Get(CancellationToken token)
    {
        var data = await _service.FineOneAsync(token);
        var model = data != null
            ? _mapper.Map<CompanyModel>(data)
            : new CompanyModel();
        model.LogoUrl = await _imageUrls.DisplayUrlAsync(data?.LogoKey, token);
        return Ok(model);
    }

    /// <summary>Uploads (or replaces) the company logo: PNG, JPG or WebP up to 2 MB.</summary>
    [HttpPost("logo")]
    [RequirePermission("Organization Setup:Edit")]
    [EnableRateLimiting(RateLimitPolicies.FileUpload)]
    [RequestSizeLimit(4 * 1024 * 1024)]
    [ProducesResponseType(typeof(ResponseModel<ImageUploadResult>), 200)]
    public async Task<IActionResult> UploadLogo(IFormFile file, CancellationToken token)
    {
        await using var upload = file.OpenReadStream();
        var url = await _images.SetCompanyLogoAsync(upload, file.Length, file.FileName, token);
        await PublishBrandingAsync(token);
        return Ok(new ImageUploadResult(url));
    }

    [HttpDelete("logo")]
    [RequirePermission("Organization Setup:Edit")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> RemoveLogo(CancellationToken token)
    {
        await _images.RemoveCompanyLogoAsync(token);
        await PublishBrandingAsync(token);
        return Ok();
    }

    [HttpPut]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> Put([FromBody] CreateCompany payload, CancellationToken token)
    {
        // "Company Name" (Description) is also the company's (tenant's) name everywhere outside
        // HRIS -- the company switcher, the access token, invitations. Rename it there first, so a
        // failure (e.g. Tenant Service rejecting the caller) leaves both sides unchanged instead of
        // HRIS showing a name nothing else knows about.
        var current = await _service.FineOneAsync(token);
        var newName = payload.Description?.Trim();
        if (!string.IsNullOrWhiteSpace(newName) &&
            !string.Equals(current?.Description?.Trim(), newName, StringComparison.Ordinal))
        {
            try
            {
                await _tenantClient.RenameCurrentAsync(newName, token);
            }
            catch (ApiException ex)
            {
                throw new InvalidOperationException(
                    $"The company name couldn't be updated ({(int)ex.StatusCode} from the company service), so nothing was saved. Please try again.", ex);
            }
        }

        // Address and contact number are also shown in the platform console, which reads them from
        // Tenant Service -- same rule as the name: sync first so a refusal saves nothing.
        var addressChanged = !string.Equals(current?.Address?.Trim() ?? "", payload.Address?.Trim() ?? "", StringComparison.Ordinal);
        var contactChanged = !string.Equals(current?.Contact?.Trim() ?? "", payload.Contact?.Trim() ?? "", StringComparison.Ordinal);
        if (addressChanged || contactChanged)
        {
            try
            {
                await _tenantClient.UpdateCurrentProfileAsync(new TenantProfileUpdate(payload.Address, payload.Contact), token);
            }
            catch (ApiException ex)
            {
                throw new InvalidOperationException(
                    $"The company details couldn't be updated ({(int)ex.StatusCode} from the company service), so nothing was saved. Please try again.", ex);
            }
        }

        await _service.SaveAsync(payload, token);
        await PublishBrandingAsync(token);
        return Ok("success");
    }

    /// <summary>The Client Portal shows the company's name and logo to its clients; it hears of a change here. Never fails the edit.</summary>
    private async Task PublishBrandingAsync(CancellationToken token)
    {
        if (_portalBranding != null)
            await _portalBranding.PublishAsync(token);
    }
}
