using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
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
    [ProducesResponseType(typeof(ResponseModel<List<DailyRecordModel>>), 200)]
    public async Task<IActionResult> Post([FromQuery] DateOnly rangeFrom, [FromQuery] DateOnly RangeTo,
        [FromBody] List<CreateDailyRecord> model, CancellationToken token)
    {
        var models = _mapper.Map<List<DailyRecord>>(model);
        var userId = User.GetRequiredUserId();
        var count = _service.Context.DailyTimeRecords.GroupBy(x => x.BatchCode).Count() + 1;
        var batchCode = $"DTR {rangeFrom.ToString("MMM-dd,yyyy")}-{RangeTo.ToString("MMM-dd,yyyy")} {count.ToString().PadLeft(10, '0')}";
        foreach (var item in models)
        {
            item.UserId = userId;
            item.BatchCode = batchCode;
        }
        await _service.AddRangeAsync(models, token);
        var respModel = _mapper.Map<List<DailyRecordModel>>(models);
        return Ok(respModel);
    }

    //[HttpPost("load-summary")]
    //[ProducesResponseType(typeof(ResponseModel<object>), 200)]
    //public async Task<IActionResult> Load([FromBody] DTRQueryPayload payload, [FromQuery] PaginationPayload pageInfo, CancellationToken token)
    //{
    //    return Ok(await _service.GetAllPaginatedResult(payload, pageInfo, token));
    //}
    [HttpPost("load-summary")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> Summary([FromQuery] string batchCode, CancellationToken token)
    {
        return Ok(await _service.DTRSummaryQuery(batchCode, token));
    }

    [HttpGet("columnar-raw")]
    [ProducesResponseType(typeof(ResponseModel<ObjectCollection<ColumnarLogModel>>), 200)]
    public async Task<IActionResult> GenerateRawColumnarView([FromQuery] DTRRequestPayload payload, CancellationToken token)
    {
        var result = await _dTRCalcService.GetDTRInfoAsync<ColumnarLogModel>(payload,
            ProcessorType.RawColumnarLog,
            token,
            IncludeNullResponse.Include);
        return Ok(result);
    }

    [HttpGet("clean-row")]
    [ProducesResponseType(typeof(ResponseModel<ObjectCollection<List<ColumnarLogModel>>>), 200)]
    public async Task<IActionResult> CleanRowView([FromQuery] DTRRequestPayload payload, CancellationToken token)
    {
        var result = await _dTRCalcService.GetDTRInfoAsync<List<RowLogModel>>(payload,
            ProcessorType.RawRowLog,
            token,
            IncludeNullResponse.Include);
        return Ok(result);
    }

    [HttpGet("clean-columnar")]
    [ProducesResponseType(typeof(ResponseModel<ObjectCollection<ColumnarLogModel>>), 200)]
    public async Task<IActionResult> CleanColumnarView([FromQuery] DTRRequestPayload payload, CancellationToken token)
    {
        var result = await _dTRCalcService.GetDTRInfoAsync<ColumnarLogModel>(payload,
            ProcessorType.CleanColumnarLog,
            token,
            IncludeNullResponse.Include);
        return Ok(result);
    }

    [HttpGet("dtr-detail")]
    [ProducesResponseType(typeof(ResponseModel<ObjectCollection<DTRDetailModel>>), 200)]
    public async Task<IActionResult> DTRDetailView([FromQuery] DTRRequestPayload payload, CancellationToken token)
    {
        var result = await _dTRCalcService.GetDTRInfoAsync<DTRDetailModel>(payload,
            ProcessorType.DTRDetail,
            token,
            IncludeNullResponse.Include);
        return Ok(result);
    }

    [HttpGet("incomplete-columnar")]
    [ProducesResponseType(typeof(ResponseModel<ObjectCollection<ColumnarLogModel>>), 200)]
    public async Task<IActionResult> IncompleteColumnarLog([FromQuery] DTRRequestPayload payload, CancellationToken token)
    {
        var result = await _dTRCalcService.GetDTRInfoAsync<ColumnarLogModel>(payload,
            ProcessorType.CleanColumnarLog,
            token,
            IncludeNullResponse.Include,false);
        return Ok(result);
    }


    [HttpDelete()]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> Delete([FromQuery] DateRangePayload payload, CancellationToken token)
    {
        await _service.DeleteAsync(payload, token);
        return Ok();
    }
}
