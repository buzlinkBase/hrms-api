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
    [ProducesResponseType(typeof(ResponseModel<List<DTRDetailModel>>), 200)]
    public async Task<IActionResult> Post([FromBody] List<DTRDetailModel> model, CancellationToken token)
    {
        var rangeFrom = model.Min(x => x.WorkDate);
        var RangeTo = model.Max(x => x.WorkDate);
        var localTime = DateTime.UtcNow.AddHours(8);
        var ts = localTime.ToString("MMddyyHHmm");
        //var rnd = Guid.CreateVersion7().ToString("N").ToString().Substring(1, 5);
        var models = _mapper.Map<List<DailyRecord>>(model);
        var userId = User.GetRequiredUserId();
        var count = _service.Context.DailyTimeRecords.GroupBy(x => x.BatchCode).Count() + 1;
        var batchCode = $"DTR{rangeFrom.ToString("MMMdd")}{RangeTo.ToString("MMMddyyyy")}-{ts}-{count.ToString().PadLeft(5, '0')}";
        foreach (var item in models)
        {
            item.UserId = userId;
            item.BatchCode = batchCode;
        }
        await _service.AddRangeAsync(models, token);
        var respModel = _mapper.Map<List<DTRDetailModel>>(models);
        return Ok(respModel);
    }

    [HttpPost("load-summary")]
    [ProducesResponseType(typeof(ResponseModel<List<DTRSummaryModel>>), 200)]
    public async Task<IActionResult> Summary([FromQuery] string batchCode, CancellationToken token)
    {
        var result = await _service.DTRSummaryQuery(batchCode, token);
        return Ok(result);
    }

    [HttpPost("load-detail")]
    [ProducesResponseType(typeof(ResponseModel<List<DTRDetailModel>>), 200)]
    public async Task<IActionResult> Details ([FromQuery] string batchCode, CancellationToken token)
    {
        var result = await _service.DTRDetailQuery(batchCode, token);
        return Ok(result);
    }

    [HttpGet("batch-codes")]
    [ProducesResponseType(typeof(ResponseModel<List<BatchesModel>>), 200)]
    public async Task<IActionResult> GetCodes(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken token)
    {
        var fromDate = from.HasValue
            ? DateOnly.FromDateTime(from.Value)
            : DateOnly.FromDateTime(DateTime.UtcNow.Date.AddMonths(-6));
        var toDate = to.HasValue
            ? DateOnly.FromDateTime(to.Value)
            : DateOnly.FromDateTime(DateTime.UtcNow.Date.AddMonths(1));
        var result = await _service.GetBatches(fromDate, toDate, token);
        return Ok(result);
    }

    [HttpPost("post")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> PostBatch([FromQuery] string batchCode, CancellationToken token)
    {
        await _service.PostAsync(batchCode, token);
        return Ok();
    }

    [HttpPost("unpost")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> UnpostBatch([FromQuery] string batchCode, CancellationToken token)
    {
        await _service.UnpostAsync(batchCode, token);
        return Ok();
    }

    [HttpGet("tardiness-report")]
    [ProducesResponseType(typeof(ResponseModel<List<TardinessReportModel>>), 200)]
    public async Task<IActionResult> TardinessReport([FromQuery] DTRRequestPayload payload, CancellationToken token)
    {
        var result = await _service.TardinessReportQuery(payload, token);
        return Ok(result);
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
            IncludeNullResponse.Include, false);
        return Ok(result);
    }

    [HttpDelete()]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    public async Task<IActionResult> DeleteBatch([FromQuery] string batchCode , CancellationToken token)
    {
        await _service.DeleteAsync(batchCode, token);
        return Ok("Success");
    }
}
