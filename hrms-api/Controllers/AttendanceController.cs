using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OnePunch.Auth.Core.Messaging;

namespace Hrms.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceService _attendanceService;
    private readonly IServiceProvider _serviceProvider;
    private readonly DailyRecordService _service;
    public AttendanceController(
        AttendanceService attendanceService,
         IServiceProvider serviceProvider,
        DailyRecordService service)
    {
        _attendanceService = attendanceService;
        _serviceProvider = serviceProvider;
        _service = service;
    }

    [HttpPost("upload-att-log")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Upload(IFormFile file,
        [FromForm] Guid? branchId,
        [FromForm] Guid? operationAreaId,
        [FromForm] Guid? clientId, 
        [FromForm] Guid? departmentId, 
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        //var BranchID  = Guid.Parse(branchId ?? "");
        //var OperationArea   = Guid.Parse(operationAreaId ?? "");

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);
        var extension = Path.GetExtension(file.FileName).ToLower();
        var parser = _serviceProvider.GetKeyedService<IFileParser>(extension);
        if (parser == null)
        {
            return BadRequest($"File type {extension} not supported");
        }

        var parsedData = await parser.Parse(memoryStream);
        foreach (var item in parsedData)
        {
            item.BranchId = branchId;
            item.ClientId = clientId;
            item.OperationAreaId = operationAreaId;
            item.DepartmentId = departmentId;
        }
        var atts = await new AttEmployeeSetter(_attendanceService.Uow)
           .ParseAttLogs(parsedData, LOGSOURCE.UPLOADED);

        await _attendanceService.AddRangeAsync(atts, ct);
        await _attendanceService.CommitChangesAsync(ct);

        return Ok("Success");
    }
}
