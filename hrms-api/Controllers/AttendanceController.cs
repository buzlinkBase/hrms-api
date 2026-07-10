using Asp.Versioning;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    private readonly EmployeeService _employeeService;
    private readonly IServiceProvider _serviceProvider;
    private readonly DailyRecordService _service;
    public AttendanceController(
                AttendanceService attendanceService,
                EmployeeService employeeService,
                DailyRecordService service,
                IServiceProvider serviceProvider
        )
    {
        _attendanceService = attendanceService;
        _employeeService = employeeService;
        _serviceProvider = serviceProvider;
        _service = service;
    }

    [HttpPost("upload-att-log")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
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
    [HttpPost("manual-entry")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ManualEntry([FromBody] List<CreateAttendance> payload, CancellationToken ct)
    {
        if (payload == null || !payload.Any())
        {
            return BadRequest("Payload cannot be empty.");
        }
        var employeeIds = payload.Select(x => x.EmployeeId).ToHashSet();
        var employees = await _service.Context.Employees
            .Where(x => x.BioId.HasValue && employeeIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                BioId = x.BioId!.Value,
                x.DepartmentId,
                x.AreaId,
                x.ClientId,
                x.PayrollGroupId,
                x.BranchId
            })
            .ToDictionaryAsync(x => x.Id, x => x, ct);

        var batchCount = await _attendanceService.Context.Attendances
            .Where(x => x.LogSource == LOGSOURCE.MANUAL)
            .Select(x => x.BatchCode)
            .Distinct()
            .CountAsync(ct);

        var batch = string.Concat("BATCH", "-", DateTime.UtcNow.Date.ToString("yyyMMdd"), "-", (batchCount + 1).ToString().PadLeft(3, '0'));

        var attendances = new List<Attendance>(payload.Count);
        foreach (var att in payload)
        {
            var key = att.EmployeeId;
            if (!employees.TryGetValue(key, out var employee))
            {
                continue;
            }
            attendances.Add(new Attendance
            {
                BatchCode = batch,
                EmployeeId = employee.Id,
                BioId = employee.BioId,
                WorkDateTime = att.WorkTime,
                BranchId = employee.BranchId,
                DepartmentId = employee.DepartmentId,
                ClientId = employee.ClientId,
                OperationAreaId = employee.AreaId,
                DeviceName = "Manual",
                IP = string.Empty,
                Boundary = null,
                LogSource = LOGSOURCE.MANUAL,
                EditRemarks = string.Empty,
            });
        }
        if (attendances.Any())
        {
            await _attendanceService.AddRangeAsync(attendances, ct);
            await _attendanceService.CommitChangesAsync(ct);
        }
        return Ok("success");
    }

    [HttpGet("generate")]
    [ProducesResponseType(typeof(ResponseModel<List<AttendanceModel>>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetManualEntry([FromQuery] AttendanceFilterDate filter, CancellationToken ct)
    {
        var result = await _attendanceService
            .GetLog(filter, LOGSOURCE.MANUAL);
        return Ok(result);
    }

    [HttpGet("raw-logs")]
    [ProducesResponseType(typeof(ResponseModel<List<AttendanceModel>>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RawRowLogs([FromQuery] AttendanceFilterDate filter, CancellationToken ct)
    {
        var result = await _attendanceService.GetRawLogs(filter);
        return Ok(result);
    }


    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        await _attendanceService.Remove(id, ct);
        await _attendanceService.CommitChangesAsync(ct);
        return Ok("success");
    }

    [HttpDelete("batch/{batch}")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete([FromRoute] string batch, CancellationToken ct)
    {
        await _attendanceService.RemoveBatch(batch, ct);
        await _attendanceService.CommitChangesAsync(ct);
        return Ok("success");
    }

}
