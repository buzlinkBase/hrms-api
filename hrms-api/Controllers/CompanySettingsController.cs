using Asp.Versioning;
using Hrms.Core.Services;
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

    public CompanySettingsController(CompanyService service, IMapper mapper, ITenantClient tenantClient)
    {
        _service = service;
        _mapper = mapper;
        _tenantClient = tenantClient;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ResponseModel<CompanyModel>), 200)]
    public async Task<IActionResult> Get(CancellationToken token)
    {
        var data = await _service.FineOneAsync(token);
        var model = data != null
            ? _mapper.Map<CompanyModel>(data)
            : new CompanyModel();
        return Ok(model);
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

        await _service.SaveAsync(payload, token);
        return Ok("success");
    }
}
