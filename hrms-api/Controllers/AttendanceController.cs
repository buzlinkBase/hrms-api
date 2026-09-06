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
    private readonly RosterReportService _rosterReportService;
    private readonly DailyRecordService _service;
    public AttendanceController(
                AttendanceService attendanceService,
                EmployeeService employeeService,
                DailyRecordService service,
                IServiceProvider serviceProvider,
                RosterReportService rosterReportService
        )
    {
        _attendanceService = attendanceService;
        _employeeService = employeeService;
        _serviceProvider = serviceProvider;
        _rosterReportService = rosterReportService;
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
        [FromForm] string remarks,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        if (string.IsNullOrWhiteSpace(remarks))
            return BadRequest("A reason/remarks for this upload is required.");

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

        // Tags the whole imported batch with why it was uploaded out-of-band — the punches
        // themselves are still device data, not hand-typed, but the upload action itself is a
        // manual intervention worth an audit trail.
        foreach (var att in atts)
        {
            att.LogRemarks = remarks;
        }

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
        if (payload.Any(x => string.IsNullOrWhiteSpace(x.Remarks)))
        {
            return BadRequest("Remarks is required for every manual attendance entry.");
        }
        var employeeIds = payload.Select(x => x.EmployeeId).ToHashSet();
        var employees = await _service.Context.Employees
            .Where(x => employeeIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.BioId,
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
                EditRemarks = att.Remarks,
            });
        }
        if (attendances.Any())
        {
            await _attendanceService.AddRangeAsync(attendances, ct);
            await _attendanceService.CommitChangesAsync(ct);
        }
        return Ok("success");
    }

    [HttpGet("tag")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Unregistered([FromQuery] TagEmployeeRequest payload, CancellationToken ct)
    {
        if (payload == null) throw new Exception("Payload cannot be empty.");
        var emp = await _employeeService.FindOne(payload.EmployeeId, ct);
        if (emp == null) throw new Exception("record not found");
        var att = await _attendanceService.FindOne(payload.AttId, ct);
        if (att == null) throw new Exception("record not found");

        if (emp.BioId != att.BioId)
        {
            throw new Exception("Bio Id in employee does not match to the attendance bioId");
        }
        await _attendanceService.Tag(att, emp, ct);
        return Ok("success");
    }

    [HttpPut()]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAtt([FromBody] UpdateAttendance payload, CancellationToken ct)
    {
        if (payload == null)
        {
            return BadRequest("Payload cannot be empty.");
        }
        if (string.IsNullOrWhiteSpace(payload.Remarks))
        {
            return BadRequest("Remarks is required.");
        }
        await _attendanceService.Update(payload, ct);
        await _attendanceService.CommitChangesAsync(ct);
        return Ok("success");
    }

    [HttpGet("unregistered")]
    [ProducesResponseType(typeof(ResponseModel<List<AttendanceModel>>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Unregistered([FromQuery] UnRegisteredAttendance filter, CancellationToken ct)
    {
        var result = await _attendanceService
            .GetUnregistered(filter);
        return Ok(result);
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

    [HttpGet("dtr-view-att-by-shift")]
    [ProducesResponseType(typeof(ResponseModel<List<AttendanceModel>>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetShiftAttendance(
        [FromQuery] Guid employeeId, [FromQuery] DateOnly workDate, [FromQuery] Guid? timeShiftId,
        [FromQuery] DateTime? actualStart, [FromQuery] DateTime? actualEnd, CancellationToken ct)
    {
        var shifts = await _rosterReportService.FindCurrentAndNextShift(workDate, employeeId, timeShiftId, ct);
        if (shifts.Current == null
            || shifts.Next == null
            || shifts.Current.StartTime == null
            || shifts.Current.EndTime == null
            || shifts.Current.ShiftType == null
            || shifts.Next.StartTime == null
            || shifts.Next.EndTime == null
            || shifts.Next.ShiftType == null)
        {
            return Ok(new List<AttendanceModel>());
        }
        var attendance = await _attendanceService.GetAllLogsInRange(employeeId, shifts.Current, shifts.Next, ct);
        return Ok(attendance);
    }

    [HttpGet("raw-logs")]
    [ProducesResponseType(typeof(ResponseModel<List<AttendanceModel>>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RawRowLogs([FromQuery] AttendanceFilter filter, CancellationToken ct)
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
