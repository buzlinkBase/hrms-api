using Onepunch.Common.Lib.Subscriptions;
using Hrms.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;
using Hrms.Api.Extensions;
using Hrms.Api.Filters;
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
[OperationsData]
[RequireFeature(EntitlementKeys.TimekeepingCore)]
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
    [EnableRateLimiting(RateLimitPolicies.FileUpload)]
    [RequirePermission("Upload Attendance:Edit")]
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
    [RequirePermission("Attendance Manual Entry:Edit")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ManualEntry([FromBody] List<CreateAttendance> payload, CancellationToken ct)
    {
        await _attendanceService.AddManualEntriesAsync(payload ?? [], ct);
        await _attendanceService.CommitChangesAsync(ct);
        return Ok("success");
    }

    // GET verb, but persists a bio-id tag (_attendanceService.Tag) -- not a read, so it's gated
    // as :Edit rather than left ungated like the other GET actions on this controller.
    [HttpGet("tag")]
    [RequirePermission("Unregistered Employees:Edit")]
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

    // Any one of the per-source edit permissions gets in; the service checks the one for the log's
    // own source (AttendanceLogSourcePolicy).
    [HttpPut()]
    [RequirePermission(AttendanceLogPermissions.Edit, AttendanceLogPermissions.EditUploaded, AttendanceLogPermissions.EditSynced)]
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
        await _attendanceService.Update(payload, LogCaller(), ct);
        await _attendanceService.CommitChangesAsync(ct);
        return Ok("success");
    }

    // Restoring an edit needs the log source's edit permission, restoring a removal its delete
    // permission -- checked by the service once it knows which.
    [HttpPost("originals/{id:guid}/restore")]
    [RequirePermission(
        AttendanceLogPermissions.EditUploaded, AttendanceLogPermissions.DeleteUploaded,
        AttendanceLogPermissions.EditSynced, AttendanceLogPermissions.DeleteSynced)]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RestoreOriginal([FromRoute] Guid id, CancellationToken ct)
    {
        await _attendanceService.RestoreOriginal(id, LogCaller(), ct);
        await _attendanceService.CommitChangesAsync(ct);
        return Ok("success");
    }

    private AttendanceLogCaller LogCaller() =>
        new(User.HasPermission, User.GetUserId(), User.GetUserClaim("email"));

    [HttpGet("unregistered")]
    [RequirePermission("Unregistered Employees:View")]
    [ProducesResponseType(typeof(ResponseModel<List<AttendanceModel>>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Unregistered([FromQuery] UnRegisteredAttendance filter, CancellationToken ct)
    {
        var result = await _attendanceService
            .GetUnregistered(filter);
        return Ok(result);
    }

    [HttpGet("generate")]
    [RequirePermission("Attendance Manual Entry:View")]
    [ProducesResponseType(typeof(ResponseModel<List<AttendanceModel>>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetManualEntry([FromQuery] AttendanceFilterDate filter, CancellationToken ct)
    {
        var result = await _attendanceService
            .GetLog(filter, LOGSOURCE.MANUAL);
        return Ok(result);
    }

    [HttpGet("dtr-view-att-by-shift")]
    [RequirePermission("Attendance Manual Entry:View")]
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

    // Removed uploaded/synced logs in the same shift window as dtr-view-att-by-shift, for Restore.
    [HttpGet("dtr-view-removed-by-shift")]
    [RequirePermission(AttendanceLogPermissions.View)]
    [ProducesResponseType(typeof(ResponseModel<List<RemovedAttendanceLogModel>>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRemovedShiftAttendance(
        [FromQuery] Guid employeeId, [FromQuery] DateOnly workDate, [FromQuery] Guid? timeShiftId, CancellationToken ct)
    {
        var shifts = await _rosterReportService.FindCurrentAndNextShift(workDate, employeeId, timeShiftId, ct);
        if (shifts.Current?.StartTime == null || shifts.Next?.StartTime == null)
        {
            return Ok(new List<RemovedAttendanceLogModel>());
        }
        var removed = await _attendanceService.GetRemovedLogsInRange(employeeId, shifts.Current, shifts.Next, ct);
        return Ok(removed);
    }

    [HttpGet("upload-batches")]
    [RequirePermission("Upload Attendance:View")]
    [ProducesResponseType(typeof(ResponseModel<List<UploadBatchModel>>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadBatches([FromQuery] UploadBatchFilter filter, CancellationToken ct)
    {
        var batches = await _attendanceService.GetUploadBatches(filter ?? new UploadBatchFilter(), ct);
        return Ok(batches);
    }

    [HttpGet("raw-logs")]
    [RequirePermission("Raw Logs:View")]
    [ProducesResponseType(typeof(ResponseModel<List<AttendanceModel>>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RawRowLogs([FromQuery] AttendanceFilter filter, CancellationToken ct)
    {
        var result = await _attendanceService.GetRawLogs(filter, ct);
        return Ok(result);
    }

    // Any one of the per-source delete permissions gets in; the service checks the one for the
    // log's own source (AttendanceLogSourcePolicy). reason is kept with a removed log's original.
    [HttpDelete("{id:guid}")]
    [RequirePermission(AttendanceLogPermissions.Delete, AttendanceLogPermissions.DeleteUploaded, AttendanceLogPermissions.DeleteSynced)]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, [FromQuery] string? reason, CancellationToken ct)
    {
        await _attendanceService.Remove(id, LogCaller(), reason, ct);
        await _attendanceService.CommitChangesAsync(ct);
        return Ok("success");
    }

    // A whole attendance file upload, with the saved originals of its edited/removed logs.
    [HttpDelete("upload-batch/{batch}")]
    [RequirePermission(AttendanceLogPermissions.DeleteUploaded)]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteUploadBatch([FromRoute] string batch, CancellationToken ct)
    {
        await _attendanceService.RemoveUploadBatch(batch, LogCaller(), ct);
        await _attendanceService.CommitChangesAsync(ct);
        return Ok("success");
    }

    [HttpDelete("batch/{batch}")]
    [RequirePermission("Attendance Manual Entry:Delete")]
    [ProducesResponseType(typeof(ResponseModel<string>), 200)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete([FromRoute] string batch, CancellationToken ct)
    {
        await _attendanceService.RemoveBatch(batch, ct);
        await _attendanceService.CommitChangesAsync(ct);
        return Ok("success");
    }

}
