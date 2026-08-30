using Asp.Versioning;
using Hrms.Core.Services;
using Hrms.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

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

    public CompanySettingsController(CompanyService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
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
        await _service.SaveAsync(payload, token);
        return Ok("success");
    }
}
