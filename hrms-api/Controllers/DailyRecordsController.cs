using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
// This controller's actions span 3 different nav groups/catalog modules -- DTR Generation (DTR
// Master/DTR Summary), Timekeeping (Raw Logs/Incomplete Punches), and Reports (Attendance
// Reports) -- so every action below is gated individually with whichever module actually owns
// it. Never add a class-level [RequirePermission] here.
public class DailyRecordsController : ControllerBase
{
    private readonly DailyRecordService _service;
    private readonly DTRCalcService _dTRCalcService;
    private readonly RosterReportService _rosterReportService;
    private readonly IMapper _mapper;
    public DailyRecordsController(DailyRecordService service,
        DTRCalcService dTRCalcService,
        RosterReportService rosterReportService,
        IMapper mapper)
    {
        _service = service;
        _dTRCalcService = dTRCalcService;
        _rosterReportService = rosterReportService;
        _mapper = mapper;
    }

    [HttpPost]
    [RequirePermission("DTR Master:Manage")]
    [ProducesResponseType(typeof(ResponseModel<List<DTRDetailModel>>), 200)]
    public async Task<IActionResult> Post([FromBody] List<DTRDetailModel> model, CancellationToken token)
    {

        var payrollGroupId = model.Select(x => x.PayrollGroupId).FirstOrDefault(x => x.HasValue);
        if (!payrollGroupId.HasValue)
            return BadRequest("Payroll Group is required to post DTR.");

        var rangeFrom = model.Min(x => x.WorkDate);
        var RangeTo = model.Max(x => x.WorkDate);
        var batchCode = await _service.BuildBatchCodeAsync(rangeFrom, RangeTo, payrollGroupId, token);
        var models = _mapper.Map<List<DailyRecord>>(model);
        var userId = User.GetRequiredUserId();
        foreach (var item in models)
        {
            item.UserId = userId;
            item.BatchCode = batchCode;
        }
        await _service.AddRangeAsync(models, token);
        var respModel = _mapper.Map<List<DTRDetailModel>>(models);
        return Ok(respModel);

    }

    // POST verb, but a pure query (DTRSummaryQuery persists nothing) -- same pattern as
    // Payroll's Calculate, gated as :View not :Manage.
    [HttpPost("load-summary")]
    [RequirePermission("DTR Summary:View")]
    [ProducesResponseType(typeof(ResponseModel<List<DTRSummaryModel>>), 200)]
    public async Task<IActionResult> Summary([FromQuery] string batchCode, CancellationToken token)
    {
        var result = await _service.DTRSummaryQuery(batchCode, token);
        return Ok(result);
    }

    // Same POST-but-query exception as Summary above.
    [HttpPost("load-detail")]
    [RequirePermission("DTR Master:View")]
    [ProducesResponseType(typeof(ResponseModel<List<DTRDetailModel>>), 200)]
    public async Task<IActionResult> Details ([FromQuery] string batchCode, CancellationToken token)
    {
        var result = await _service.DTRDetailQuery(batchCode, token);
        return Ok(result);
    }

    // Shared by both the DTR Master and DTR Summary screens -- any-of gate, either permission
    // lets the call through.
    [HttpGet("batch-codes")]
    [RequirePermission("DTR Master:View", "DTR Summary:View")]
    [ProducesResponseType(typeof(ResponseModel<List<BatchesModel>>), 200)]
    public async Task<IActionResult> GetCodes(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken token)
    {
        var fromDate = from.HasValue
            ? DateOnly.FromDateTime(from.Value)
            : DateOnly.FromDateTime(DateTime.UtcNow.Date.AddMonths(-5));
        var toDate = to.HasValue
            ? DateOnly.FromDateTime(to.Value)
            : DateOnly.FromDateTime(DateTime.UtcNow.Date.AddMonths(1));
        var result = await _service.GetBatches(fromDate, toDate, token);
        return Ok(result);
    }

    // No frontend caller found for PostBatch/UnpostBatch (dead today), gated defensively anyway.
    [HttpPost("post")]
    [RequirePermission("DTR Summary:Manage")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> PostBatch([FromQuery] string batchCode, CancellationToken token)
    {
        await _service.PostAsync(batchCode, token);
        return Ok();
    }

    [HttpPost("unpost")]
    [RequirePermission("DTR Summary:Manage")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> UnpostBatch([FromQuery] string batchCode, CancellationToken token)
    {
        await _service.UnpostAsync(batchCode, token);
        return Ok();
    }

    // Reports module, not DTR Generation -- consumed only by the Tardiness report screen.
    [HttpGet("tardiness-report")]
    [RequirePermission("Attendance Reports:View")]
    [ProducesResponseType(typeof(ResponseModel<List<TardinessReportModel>>), 200)]
    public async Task<IActionResult> TardinessReport([FromQuery] DTRRequestPayload payload, CancellationToken token)
    {
        var result = await _service.TardinessReportQuery(payload, token);
        return Ok(result);
    }

    // Reports module, not DTR Generation -- consumed only by the Rostering report screen.
    [HttpGet("roster-report")]
    [RequirePermission("Attendance Reports:View")]
    [ProducesResponseType(typeof(ResponseModel<List<RosterReportModel>>), 200)]
    public async Task<IActionResult> RosterReport([FromQuery] DTRRequestPayload payload, CancellationToken token)
    {
        var result = await _rosterReportService.RosterReportQuery(payload, token);
        return Ok(result);
    }

    // Timekeeping's Raw Logs screen, not DTR Generation.
    [HttpGet("columnar-raw")]
    [RequirePermission("Raw Logs:View")]
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
    [RequirePermission("Raw Logs:View")]
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
    [RequirePermission("Raw Logs:View")]
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
    [RequirePermission("DTR Master:View")]
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
    [RequirePermission("Incomplete Punches:View")]
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
    [RequirePermission("DTR Master:Manage")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    public async Task<IActionResult> DeleteBatch([FromQuery] string batchCode , CancellationToken token)
    {
        await _service.DeleteAsync(batchCode, token);
        return Ok("Success");
    }
}
