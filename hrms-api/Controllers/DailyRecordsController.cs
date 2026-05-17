using Asp.Versioning;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
public class DailyRecordsController : ControllerBase
{
    private readonly DailyRecordService _service;
    private readonly DTRCalcService _dTRCalcService;
    private readonly IMapper _mapper; 
    public DailyRecordsController(DailyRecordService service,
        DTRCalcService dTRCalcService,
        IMapper mapper)
    {
        _service = service;
        _dTRCalcService = dTRCalcService;
        _mapper = mapper;
    }


    [HttpPost]
    public async Task<IActionResult> Post([FromBody] List<CreateDailyRecord> model, CancellationToken token)
    {
        var models = _mapper.Map<List<DailyRecord>>(model);
        await _service.AddRangeAsync(models, token);
        var respModel = _mapper.Map<CostCenterModel>(models);
        return Ok(respModel);
    }

    [HttpPost("load-summary")]
    public async Task<IActionResult> Load([FromBody] DTRQueryPayload payload, [FromQuery] PaginationPayload pageInfo, CancellationToken token)
    {
        return Ok(await _service.GetAllPaginatedResult(payload, pageInfo, token));
    }

    [HttpPost("generate")]
    [AllowAnonymous]
    public async Task<IActionResult> Generate([FromBody] DTRRequestPayload payload,  CancellationToken token)
    {
        var result = await _dTRCalcService.GetDTRInfoAsync<DailyRecord>(payload,
            ProcessorType.DTRDetail,
            token,
            IncludeNullResponse.Include);
        return Ok(result);
    }

    [HttpDelete()]
    public async Task<IActionResult> Delete([FromQuery] DateRangePayload payload, CancellationToken token)
    {
        await _service.DeleteAsync(payload, token);
        return Ok();
    }
}

