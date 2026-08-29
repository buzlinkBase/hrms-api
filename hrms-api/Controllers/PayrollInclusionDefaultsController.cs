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
public class PayrollInclusionDefaultsController : ControllerBase
{
    private readonly PayrollInclusionDefaultsService _service;
    private readonly IMapper _mapper;

    public PayrollInclusionDefaultsController(PayrollInclusionDefaultsService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ResponseModel<PayrollInclusionDefaultsModel>), 200)]
    public async Task<IActionResult> Get(CancellationToken token)
    {
        var data = await _service.FineOneAsync(token);
        var model = data != null
            ? _mapper.Map<PayrollInclusionDefaultsModel>(data)
            : new PayrollInclusionDefaultsModel();
        return Ok(model);
    }

    [HttpPut]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> Put([FromBody] UpdatePayrollInclusionDefaults payload, CancellationToken token)
    {
        await _service.SaveAsync(payload, token);
        return Ok("success");
    }
}
