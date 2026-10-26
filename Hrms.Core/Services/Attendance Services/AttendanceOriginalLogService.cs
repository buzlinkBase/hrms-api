using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

/// <summary>
/// Keeps the original of an uploaded/synced attendance log a user edited or removed
/// (<see cref="AttendanceOriginalLog"/>), restores it, and tells the insert path which incoming
/// punches those originals block. Works inside the caller's unit of work; the caller commits.
/// </summary>
public class AttendanceOriginalLogService
{
    private readonly IUnitOfWorkService _uow;

    public AttendanceOriginalLogService(IUnitOfWorkService uow)
    {
        _uow = uow;
    }

    private HrmsContext Context => _uow.Context;

    /// <summary>Before an edit: saves the log as it is now -- unless an earlier edit already saved the true original.</summary>
    public async Task RecordEditAsync(Attendance log, string reason, AttendanceLogCaller caller, CancellationToken token)
    {
        if (await Context.AttendanceOriginalLogs.AnyAsync(x => x.AttendanceId == log.Id, token))
            return;

        var original = AttendanceOriginalLog.From(log);
        original.Action = ATTENDANCE_ORIGINAL_ACTION.EDITED;
        original.AttendanceId = log.Id;
        Stamp(original, reason, caller);
        Context.AttendanceOriginalLogs.Add(original);
    }

    /// <summary>
    /// Before a removal: an edited log's saved original becomes the removed one (the edit goes with
    /// the log); otherwise the log as it is now is saved.
    /// </summary>
    public async Task RecordDeleteAsync(Attendance log, string reason, AttendanceLogCaller caller, CancellationToken token)
    {
        var original = await Context.AttendanceOriginalLogs.FirstOrDefaultAsync(x => x.AttendanceId == log.Id, token);
        if (original == null)
        {
            original = AttendanceOriginalLog.From(log);
            Context.AttendanceOriginalLogs.Add(original);
        }

        original.Action = ATTENDANCE_ORIGINAL_ACTION.DELETED;
        original.AttendanceId = null;
        Stamp(original, reason, caller);
    }

    /// <summary>
    /// Puts the original back -- an edited log gets its original time and remarks again, a removed
    /// one is re-created -- and drops the saved original. Restoring an edit needs the source's edit
    /// permission; restoring a removal, its delete permission. Returns the punch times the portal
    /// must re-send (old and new).
    /// </summary>
    public async Task<List<(Guid? EmployeeId, DateTime At)>> RestoreAsync(Guid originalId, AttendanceLogCaller caller, CancellationToken token)
    {
        var original = await Context.AttendanceOriginalLogs.FirstOrDefaultAsync(x => x.Id == originalId, token)
            ?? throw new NotFoundException("Original attendance log not found.");

        var changed = new List<(Guid? EmployeeId, DateTime At)> { (original.EmployeeId, original.WorkDateTime) };
        var live = original.AttendanceId == null
            ? null
            : await Context.Attendances.FirstOrDefaultAsync(x => x.Id == original.AttendanceId, token);

        if (original.Action == ATTENDANCE_ORIGINAL_ACTION.EDITED && live != null)
        {
            AttendanceLogSourcePolicy.EnsureCanEdit(original.LogSource, caller);
            changed.Add((live.EmployeeId, live.WorkDateTime));
            live.WorkDateTime = original.WorkDateTime;
            live.EditRemarks = original.EditRemarks;
        }
        else
        {
            AttendanceLogSourcePolicy.EnsureCanDelete(original.LogSource, caller);
            var alreadyThere = await Context.Attendances.AnyAsync(x =>
                x.BioId == original.BioId &&
                x.EmployeeId == original.EmployeeId &&
                x.WorkDateTime == original.WorkDateTime &&
                x.LogSource == original.LogSource, token);
            if (!alreadyThere)
                Context.Attendances.Add(original.ToAttendance());
        }

        Context.AttendanceOriginalLogs.Remove(original);
        return changed;
    }

    /// <summary>Removed (not edited) originals of one employee's logs between from (inclusive) and to (exclusive).</summary>
    public async Task<List<RemovedAttendanceLogModel>> ListRemovedInRangeAsync(Guid employeeId, DateTime from, DateTime to, CancellationToken token)
    {
        return await Context.AttendanceOriginalLogs
            .AsNoTracking()
            .Where(x => x.Action == ATTENDANCE_ORIGINAL_ACTION.DELETED &&
                        x.EmployeeId == employeeId &&
                        x.WorkDateTime >= from &&
                        x.WorkDateTime < to)
            .OrderBy(x => x.WorkDateTime)
            .Select(x => new RemovedAttendanceLogModel
            {
                Id = x.Id,
                WorkDateTime = x.WorkDateTime,
                LogSource = x.LogSource.ToString(),
                Batch = x.BatchCode,
                DeviceName = x.DeviceName,
                Reason = x.Reason,
                RemovedBy = x.ActedByName,
                RemovedAt = x.ActedAt,
            })
            .ToListAsync(token);
    }

    /// <summary>The edited/removed logs of one upload batch with who changed them, oldest punch first.</summary>
    public async Task<List<UploadBatchChangeModel>> ListForUploadBatchAsync(string batch, CancellationToken token)
    {
        var originals = await Context.AttendanceOriginalLogs
            .AsNoTracking()
            .Where(x => x.BatchCode == batch && x.BatchCode != "" && x.LogSource == LOGSOURCE.UPLOADED)
            .OrderBy(x => x.WorkDateTime)
            .ToListAsync(token);
        if (originals.Count == 0) return [];

        var employeeIds = originals.Where(x => x.EmployeeId != null).Select(x => x.EmployeeId!.Value).Distinct().ToList();
        var names = await Context.Employees.AsNoTracking()
            .Where(x => employeeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.LastName, x.FirstName })
            .ToDictionaryAsync(x => x.Id, x => $"{x.LastName}, {x.FirstName}".Trim(' ', ','), token);

        var liveIds = originals.Where(x => x.AttendanceId != null).Select(x => x.AttendanceId!.Value).ToList();
        var current = await Context.Attendances.AsNoTracking()
            .Where(x => liveIds.Contains(x.Id))
            .Select(x => new { x.Id, x.WorkDateTime })
            .ToDictionaryAsync(x => x.Id, x => x.WorkDateTime, token);

        return originals.Select(x => new UploadBatchChangeModel
        {
            Id = x.Id,
            Action = x.Action.ToString(),
            EmployeeId = x.EmployeeId,
            EmployeeName = x.EmployeeId != null && names.TryGetValue(x.EmployeeId.Value, out var name) ? name : string.Empty,
            BioId = x.BioId,
            OriginalDateTime = x.WorkDateTime,
            CurrentDateTime = x.AttendanceId != null && current.TryGetValue(x.AttendanceId.Value, out var at) ? at : null,
            Reason = x.Reason,
            ChangedBy = x.ActedByName,
            ChangedAt = PhTime.AsUtc(x.ActedAt),
        }).ToList();
    }

    /// <summary>Drops every saved original of one upload batch, so re-importing the file starts clean.</summary>
    public async Task RemoveForUploadBatchAsync(string batch, CancellationToken token)
    {
        // Confidential employees' originals too: the batch goes as a whole.
        await Context.IgnoringConfidentiality<AttendanceOriginalLog>()
            .Where(x => x.BatchCode == batch && x.LogSource == LOGSOURCE.UPLOADED)
            .ExecuteDeleteAsync(token);
    }

    /// <summary>
    /// The (BioId, EmployeeId, WorkDateTime) of every saved original for these Bio IDs in the range --
    /// an incoming uploaded/synced punch with one of these keys was edited or removed by a user and
    /// must not be imported again.
    /// </summary>
    public async Task<HashSet<(int? BioId, Guid? EmployeeId, DateTime WorkDateTime)>> FindOriginalKeysAsync(
        List<int?> bioIds, DateTime from, DateTime to, CancellationToken token)
    {
        // Confidential employees' originals too: an upload must not bring back a punch whose
        // employee the uploader can't see.
        var keys = await Context.IgnoringConfidentiality<AttendanceOriginalLog>()
            .AsNoTracking()
            .Where(x => bioIds.Contains(x.BioId) && x.WorkDateTime >= from && x.WorkDateTime <= to)
            .Select(x => new { x.BioId, x.EmployeeId, x.WorkDateTime })
            .ToListAsync(token);
        return keys.Select(x => (x.BioId, x.EmployeeId, x.WorkDateTime)).ToHashSet();
    }

    private static void Stamp(AttendanceOriginalLog original, string reason, AttendanceLogCaller caller)
    {
        original.Reason = reason?.Trim() ?? string.Empty;
        original.ActedByUserId = caller.UserId;
        original.ActedByName = caller.Name ?? string.Empty;
        original.ActedAt = DateTime.UtcNow;
    }
}
