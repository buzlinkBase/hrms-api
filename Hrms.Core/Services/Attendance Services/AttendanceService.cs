using Castle.Components.DictionaryAdapter.Xml;
using DTR.Core;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Core.Services.Subscriptions;
using Polly;

namespace Hrms.Core.Services;

public class AttendanceService : BaseService<Attendance>
{
    private readonly IAttendanceSourceGuard? _sourceGuard;
    private readonly ICompanyWriteGuard? _writeGuard;
    private readonly ClientPortal.ClientPortalPunchChangeService? _portalPunches;
    private readonly AttendanceOriginalLogService _originals;

    /// <param name="sourceGuard">Refuses logs from a source the company's plan doesn't include (mobile
    /// attendance). Supplied by DI; omitted only where a plan check is not wanted (unit tests).</param>
    /// <param name="portalPunches">Re-sends the posted days an edited or deleted punch belongs to, to the
    /// Client Portal. Supplied by DI; omitted in unit tests that don't care about the portal.</param>
    /// <param name="writeGuard">Refuses changes while the company is read-only (device punches follow the
    /// punch-retention rule). Supplied by DI; omitted in unit tests that don't care about the subscription.</param>
    public AttendanceService(IUnitOfWorkService uow, IAttendanceSourceGuard? sourceGuard = null, ClientPortal.ClientPortalPunchChangeService? portalPunches = null, ICompanyWriteGuard? writeGuard = null) : base(uow)
    {
        _sourceGuard = sourceGuard;
        _writeGuard = writeGuard;
        _portalPunches = portalPunches;
        _originals = new AttendanceOriginalLogService(uow);
    }

    /// <summary>
    /// Edits a log's time. Each source needs its own permission (AttendanceLogSourcePolicy); editing
    /// an uploaded/synced log first saves its original, for Restore and so a re-sync won't re-add it.
    /// </summary>
    public async Task Update(UpdateAttendance model, AttendanceLogCaller caller, CancellationToken token)
    {
        await EnsureWritableAsync(token);
        var before = await Context.Attendances.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.Id, token)
            ?? throw new NotFoundException("Attendance log not found.");

        var rule = AttendanceLogSourcePolicy.EnsureCanEdit(before.LogSource, caller);
        if (rule.KeepsOriginal)
            await _originals.RecordEditAsync(before, model.Remarks, caller, token);

        await Context.Attendances
            .Where(x => x.Id == model.Id)
            .ExecuteUpdateAsync(x => x
                .SetProperty(xx => xx.WorkDateTime, model.WorkTime)
                .SetProperty(xx => xx.EditRemarks, model.Remarks), token)
            ;

        // The punch may have moved to another day, so both the old and the new day are sent again.
        await StagePortalRefreshAsync([(before.EmployeeId, before.WorkDateTime), (before.EmployeeId, model.WorkTime)], token);
    }

    /// <summary>
    /// Puts an edited or removed uploaded/synced log back the way the file/device had it (see
    /// AttendanceOriginalLogService.RestoreAsync).
    /// </summary>
    public async Task RestoreOriginal(Guid originalId, AttendanceLogCaller caller, CancellationToken token)
    {
        await EnsureWritableAsync(token);
        var changed = await _originals.RestoreAsync(originalId, caller, token);
        await StagePortalRefreshAsync(changed, token);
    }

    private Task EnsureWritableAsync(CancellationToken token) =>
        _writeGuard?.EnsureWritableAsync(token) ?? Task.CompletedTask;

    // Staged in the caller's unit of work: leaves with its commit (see ClientPortalPunchChangeService).
    private async Task StagePortalRefreshAsync(IReadOnlyCollection<(Guid? EmployeeId, DateTime At)> punches, CancellationToken token)
    {
        if (_portalPunches != null && punches.Count > 0)
            await _portalPunches.StageAsync(punches, token);
    }

    public async Task AddRangeAsync(List<Attendance> attendances, CancellationToken token = default)
    {
        if (attendances == null || !attendances.Any())
        {
            return;
        }

        // Before anything is deleted or stored: a batch holding a log from a source the plan lacks is refused whole.
        if (_sourceGuard != null)
            await _sourceGuard.EnsureAllowedAsync(attendances.Select(x => x.LogSource), token);
        if (_writeGuard != null)
            await _writeGuard.EnsureCanStoreAttendanceAsync(attendances.Select(x => x.LogSource), token);

        var toInsert = await RemoveExistingDuplicatesAsync(attendances, token);

        // 4. Prepare the incoming records for clean insertions
        foreach (var attendance in toInsert)
        {
            // Resetting the ID allows the database to generate a fresh, non-conflicting primary key
            attendance.Id = Guid.Empty;
            attendance.UserName = string.Empty;
            //attendance.UserId = _currentUser.Id;
        }
        await CreateRangeAsync(toInsert, token);
    }

    // Ids per DELETE ... WHERE Id IN (...) -- keeps each statement's parameter list bounded.
    private const int DeleteChunkSize = 1000;

    /// <summary>
    /// An incoming log replaces any stored log with the same (BioId, EmployeeId, WorkDateTime).
    /// Set-based on purpose: one ranged lookup + a few Id deletes. A per-key DELETE (one round
    /// trip and one table scan per log) took past the client's timeout on a ~1600-row upload.
    ///
    /// A user's change to an uploaded/synced log sticks: an incoming uploaded/synced punch is
    /// skipped when its key matches a saved original (the log was edited or removed) or an edited
    /// log, and an edited log is never replaced. Manual entries are always added. Returns the logs
    /// to insert.
    /// </summary>
    private async Task<List<Attendance>> RemoveExistingDuplicatesAsync(List<Attendance> attendances, CancellationToken token)
    {
        var incomingKeys = attendances
            .Select(x => (x.BioId, x.EmployeeId, x.WorkDateTime))
            .ToHashSet();
        var bioIds = incomingKeys.Select(x => x.BioId).Distinct().ToList();
        var from = incomingKeys.Min(x => x.WorkDateTime);
        var to = incomingKeys.Max(x => x.WorkDateTime);

        var candidates = await GetQueryable()
            .Where(x => bioIds.Contains(x.BioId) && x.WorkDateTime >= from && x.WorkDateTime <= to)
            .Select(x => new
            {
                x.Id, x.BioId, x.EmployeeId, x.WorkDateTime,
                Edited = Context.AttendanceOriginalLogs.Any(o => o.AttendanceId == x.Id),
            })
            .ToListAsync(token);

        var blockedKeys = await _originals.FindOriginalKeysAsync(bioIds, from, to, token);
        foreach (var edited in candidates.Where(x => x.Edited))
            blockedKeys.Add((edited.BioId, edited.EmployeeId, edited.WorkDateTime));

        var toInsert = attendances
            .Where(x => x.LogSource == LOGSOURCE.MANUAL || !blockedKeys.Contains((x.BioId, x.EmployeeId, x.WorkDateTime)))
            .ToList();
        var insertKeys = toInsert.Select(x => (x.BioId, x.EmployeeId, x.WorkDateTime)).ToHashSet();

        // Matched in memory so a null EmployeeId (unregistered Bio ID) only matches null.
        var staleIds = candidates
            .Where(x => !x.Edited && insertKeys.Contains((x.BioId, x.EmployeeId, x.WorkDateTime)))
            .Select(x => x.Id)
            .ToList();

        foreach (var chunk in staleIds.Chunk(DeleteChunkSize))
        {
            await GetQueryable()
                .Where(x => chunk.Contains(x.Id))
                .ExecuteDeleteAsync(token);
        }
        return toInsert;
    }

    /// <summary>
    /// Attendance Manual Entry (and a time log added from the DTR). Every entry must be for an
    /// employee the caller may see -- a hidden confidential employee, or one of another company,
    /// is refused rather than silently skipped -- and any branch/client/project site/department
    /// chosen for it must be one of this company's. Flushes; the caller commits.
    /// </summary>
    public async Task<string> AddManualEntriesAsync(List<CreateAttendance> entries, CancellationToken token)
    {
        if (entries.Count == 0)
            throw new ValidationException("Add at least one attendance entry.");
        if (entries.Any(x => string.IsNullOrWhiteSpace(x.Remarks)))
            throw new ValidationException("Remarks is required for every manual attendance entry.");

        var employeeIds = entries.Select(x => x.EmployeeId).ToHashSet();
        var employees = await Context.Employees
            .Where(x => employeeIds.Contains(x.Id))
            .Select(x => new ManualAttendanceEmployee(x.Id, x.BioId, x.BranchId, x.DepartmentId, x.ClientId, x.AreaId))
            .ToDictionaryAsync(x => x.Id, token);
        if (employeeIds.Any(id => !employees.ContainsKey(id)))
            throw new ValidationException("You don't have access to one or more of the selected employees.");

        await EnsureAllExistAsync(Context.Branches, entries.Select(x => x.BranchId), "branch", token);
        await EnsureAllExistAsync(Context.Clients, entries.Select(x => x.ClientId), "client", token);
        await EnsureAllExistAsync(Context.Areas, entries.Select(x => x.OperationAreaId), "project site", token);
        await EnsureAllExistAsync(Context.Departments, entries.Select(x => x.DepartmentId), "department", token);

        var batchCount = await Context.Attendances
            .Where(x => x.LogSource == LOGSOURCE.MANUAL)
            .Select(x => x.BatchCode)
            .Distinct()
            .CountAsync(token);
        var batch = string.Concat("BATCH", "-", DateTime.UtcNow.Date.ToString("yyyMMdd"), "-", (batchCount + 1).ToString().PadLeft(3, '0'));

        var branchOfArea = await AttendanceLocationResolver.LoadBranchOfAreaAsync(Context,
            entries.Select(x => x.OperationAreaId).Concat(employees.Values.Select(x => x.AreaId)), token);
        await AddRangeAsync(entries.Select(x => ManualAttendanceFactory.Create(x, employees[x.EmployeeId], batch, branchOfArea)).ToList(), token);
        return batch;
    }

    // Each chosen id must be a live record of this company (the query filters scope it).
    private static async Task EnsureAllExistAsync<TEntity>(IQueryable<TEntity> set, IEnumerable<Guid?> chosen, string what, CancellationToken token)
        where TEntity : class, IEntity
    {
        var ids = chosen.Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        if (ids.Count == 0) return;
        var found = await set.CountAsync(x => ids.Contains(x.Id), token);
        if (found != ids.Count)
            throw new ValidationException($"The selected {what} was not found.");
    }

    public async Task AddRange(List<Attendance> models, CancellationToken token)
    {
        if (_writeGuard != null)
            await _writeGuard.EnsureCanStoreAttendanceAsync(models.Select(x => x.LogSource), token);
        await CreateRangeAsync(models, token);
    }

    public async Task<List<AttendanceModel>> GetLog(AttendanceFilterDate filter, LOGSOURCE source = LOGSOURCE.MANUAL)
    {
        DateTime fromDate = (filter?.FromDate ?? DateOnly.MinValue).ToDateTime(TimeOnly.MinValue);
        DateTime toDate = (filter?.ToDate ?? DateOnly.MinValue).ToDateTime(TimeOnly.MinValue).AddDays(1);
        Guid? filterEmployeeId = filter?.EmployeeId;

        return await Uow.Context.Attendances
            .Where(x => x.LogSource == source &&
                        x.WorkDateTime >= fromDate &&
                        x.WorkDateTime < toDate &&
                        (filterEmployeeId == null || x.EmployeeId == filterEmployeeId))
            .Select(x => new AttendanceModel
            {
                Id = x.Id,
                BioId = x.BioId,
                Batch = x.BatchCode,
                EmployeeId = x.EmployeeId,
                WorkDateTime = x.WorkDateTime,
                LogSource = x.LogSource.ToString(),
                LogRemarks = x.LogRemarks,
                EditRemarks = x.EditRemarks,
                Area = x.OperationArea != null ? x.OperationArea.Name : null,
                Branch = x.Branch != null ? x.Branch.Name : null,
                Client = x.Client != null ? x.Client.Name : null,
                Name = x.Employee != null
                    ? (x.Employee.LastName ?? "") + ", " + (x.Employee.FirstName ?? "") + " " + (x.Employee.MiddleName ?? "") + " " + (x.Employee.Suffix ?? "")
                    : "",
            })
            .OrderByDescending(x => x.Name)
            .ThenBy(x => x.WorkDateTime)
            .ToListAsync();
    }

    // Same window the DTR captures for this shift (DTR.Core AttendanceProvider), including the
    // earliest allowed clock-in before the shift start — otherwise an early time-in the DTR
    // counted would be missing from the DTR Attendance Logs view.
    private async Task<(DateTime From, DateTime To)> ShiftCaptureWindowAsync(CurrentShiftInfo current, CurrentShiftInfo next, CancellationToken ct)
    {
        var settings = await Context.GeneralSettings.FirstOrDefaultAsync(
            x => x.IdentityType == "Company" && x.Description == nameof(SettingKey.TimeInAllowance), ct);
        var window = AttendanceCaptureWindow.For(
            current.ShiftType, current.StartTime!.Value, next.StartTime!.Value,
            GeneralSettingsUtil.ParseInt(settings?.Value, -120));
        return (window.From, window.To);
    }

    /// <summary>Removed uploaded/synced logs of the shift (DTR Attendance Logs view), for Restore.</summary>
    public async Task<List<RemovedAttendanceLogModel>> GetRemovedLogsInRange(Guid employeeId, CurrentShiftInfo current, CurrentShiftInfo next, CancellationToken ct)
    {
        var (from, to) = await ShiftCaptureWindowAsync(current, next, ct);
        var removed = await _originals.ListRemovedInRangeAsync(employeeId, from, to, ct);
        foreach (var log in removed)
            log.RemovedAt = PhTime.AsUtc(log.RemovedAt);
        return removed;
    }

    public Task<List<UploadBatchChangeModel>> GetUploadBatchChanges(string batch, CancellationToken ct) =>
        _originals.ListForUploadBatchAsync(batch, ct);

    public async Task<List<AttendanceModel>> GetAllLogsInRange(Guid employeeId, CurrentShiftInfo current, CurrentShiftInfo next, CancellationToken ct)
    {
        var (from, to) = await ShiftCaptureWindowAsync(current, next, ct);

        return await Uow.Context.Attendances
            .Where(x => x.WorkDateTime >= from &&
                        x.WorkDateTime < to &&
                        x.EmployeeId == employeeId)
            .Select(x => new AttendanceModel
            {
                Id = x.Id,
                BioId = x.BioId,
                Batch = x.BatchCode,
                EmployeeId = x.EmployeeId,
                WorkDateTime = x.WorkDateTime,
                LogSource = x.LogSource.ToString(),
                LogRemarks = x.LogRemarks,
                EditRemarks = x.EditRemarks,
                Area = x.OperationArea != null ? x.OperationArea.Name : null,
                Branch = x.Branch != null ? x.Branch.Name : null,
                Client = x.Client != null ? x.Client.Name : null,
                Name = x.Employee != null
                    ? (x.Employee.LastName ?? "") + ", " + (x.Employee.FirstName ?? "") + " " + (x.Employee.MiddleName ?? "") + " " + (x.Employee.Suffix ?? "")
                    : "",
                OriginalLogId = Uow.Context.AttendanceOriginalLogs
                    .Where(o => o.AttendanceId == x.Id).Select(o => (Guid?)o.Id).FirstOrDefault(),
                OriginalWorkDateTime = Uow.Context.AttendanceOriginalLogs
                    .Where(o => o.AttendanceId == x.Id).Select(o => (DateTime?)o.WorkDateTime).FirstOrDefault(),
            })
            .OrderByDescending(x => x.Name)
            .ThenBy(x => x.WorkDateTime)
            .ToListAsync(ct);
    }


    public async Task<Attendance?> FindOne(Guid attId, CancellationToken token)
    {
        return await Context.Attendances.FindAsync(attId, token);
    }
    public async Task Tag(Attendance attendance, Employee employee, CancellationToken token)
    {
        await EnsureWritableAsync(token);
        // Only the unregistered logs (no employee yet) -- logs already tagged stay with whoever
        // they belong to, e.g. a former employee who held this Bio ID before it was reassigned.
        var logs = await Context.Attendances
            .Where(x => x.BioId.HasValue && x.BioId == employee.BioId && x.EmployeeId == null)
            .ToListAsync(token);
        if (logs.Count == 0) return;

        // Each log keeps the device's location; its blanks are filled from the employee's own.
        var own = new AttendanceLocation(employee.BranchId, employee.ClientId, employee.AreaId, employee.DepartmentId);
        var branchOfArea = await AttendanceLocationResolver.LoadBranchOfAreaAsync(Context,
            logs.Select(x => x.OperationAreaId).Append(employee.AreaId), token);
        foreach (var log in logs)
        {
            var location = AttendanceLocationResolver.Resolve(
                new AttendanceLocation(log.BranchId, log.ClientId, log.OperationAreaId, log.DepartmentId), own, branchOfArea);
            log.EmployeeId = employee.Id;
            log.BranchId = location.BranchId;
            log.ClientId = location.ClientId;
            log.OperationAreaId = location.OperationAreaId;
            log.DepartmentId = location.DepartmentId;
        }

        await CommitChangesAsync(token);

    }
    public async Task<List<AttendanceModel>> GetUnregistered(UnRegisteredAttendance filter, LOGSOURCE source = LOGSOURCE.MANUAL)
    {
        DateTime fromDate = (filter?.FromDate ?? DateOnly.MinValue).ToDateTime(TimeOnly.MinValue);
        DateTime toDate = (filter?.ToDate ?? DateOnly.MinValue).ToDateTime(TimeOnly.MinValue).AddDays(1);

        return await Uow.Context.Attendances
            .Where(x => x.WorkDateTime >= fromDate &&
                            x.WorkDateTime < toDate &&
                            x.EmployeeId == null)
            .Select(x => new AttendanceModel
            {
                Id = x.Id,
                BioId = x.BioId,
                Batch = x.BatchCode,
                EmployeeId = x.EmployeeId,
                WorkDateTime = x.WorkDateTime,
                LogSource = x.LogSource.ToString(),
                LogRemarks = x.LogRemarks,
                EditRemarks = x.EditRemarks,
                Area = x.OperationArea != null ? x.OperationArea.Name : null,
                Branch = x.Branch != null ? x.Branch.Name : null,
                Client = x.Client != null ? x.Client.Name : null,
                Name = x.Employee != null
                    ? (x.Employee.LastName ?? "") + ", " + (x.Employee.FirstName ?? "") + " " + (x.Employee.MiddleName ?? "") + " " + (x.Employee.Suffix ?? "")
                    : "",
            })
            .OrderByDescending(x => x.BioId)
            .ThenBy(x => x.WorkDateTime)
            .ToListAsync();
    }

    public async Task<List<AttendanceModel>> GetRawLogs(AttendanceFilter filter, CancellationToken token = default)
    {
        DateTime fromDate = (filter?.FromDate ?? DateOnly.MinValue).ToDateTime(TimeOnly.MinValue);
        DateTime toDate = (filter?.ToDate ?? DateOnly.MinValue).ToDateTime(TimeOnly.MinValue).AddDays(1);

        Guid? EmployeeId = filter?.EmployeeId;
        Guid? BranchId = filter?.BranchId;
        Guid? DepartmentId = filter?.DepartmentId;
        Guid? ClientId = filter?.ClientId;
        Guid? PayrollGroupId = filter?.PayrollGroupId;
        Guid? OperationAreaId = filter?.OperationAreaId;


        return await Uow.Context.Attendances
            .AsNoTracking()
            .Where(x => x.WorkDateTime >= fromDate && x.WorkDateTime < toDate &&
                        (EmployeeId == null || x.EmployeeId == EmployeeId) &&
                        (BranchId == null || x.BranchId == BranchId) &&
                        (DepartmentId == null || x.DepartmentId == DepartmentId) &&
                        (ClientId == null || x.ClientId == ClientId) &&
                        // Logs carry no payroll group of their own -- it's the employee's.
                        (PayrollGroupId == null || (x.Employee != null && x.Employee.PayrollGroupId == PayrollGroupId)) &&
                        (OperationAreaId == null || x.OperationAreaId == OperationAreaId))
            .Select(x => new AttendanceModel
            {
                Id = x.Id,
                BioId = x.BioId,
                Batch = x.BatchCode,
                EmployeeId = x.EmployeeId,
                WorkDateTime = x.WorkDateTime,
                LogSource = x.LogSource.ToString(),
                LogRemarks = x.LogRemarks,
                EditRemarks = x.EditRemarks,
                Area = x.OperationArea != null ? x.OperationArea.Name : null,
                Branch = x.Branch != null ? x.Branch.Name : null,
                Client = x.Client != null ? x.Client.Name : null,
                Name = x.Employee != null
                    ? (x.Employee.LastName ?? "") + ", " + (x.Employee.FirstName ?? "") + " " + (x.Employee.MiddleName ?? "") + " " + (x.Employee.Suffix ?? "")
                    : "",
            })
            .OrderByDescending(x => x.Name)
            .ThenBy(x => x.WorkDateTime)
            .ToListAsync(token);
    }

    private static (DateOnly fromDate, DateOnly toDate) GetDateRange(DTRRequestPayload payload)
    {
        var from = payload.FromDate.AddDays(TimeAllowance.AttLookbackDays);
        var to = payload.ToDate.AddDays(TimeAllowance.AttLookforward);
        return (from, to);
    }
    public async Task<Dictionary<AttendanceEmpId, List<Attendance>>> LoadAttForDTRProcess(
        DTRRequestPayload payload,
        HashSet<Guid> empIds,
        bool canProcess,
        CancellationToken token)
    {
        var departmentId = payload.DepartmentId;
        var payrollGroupId = payload.PayrollGroupId;
        var branchId = payload.BranchId;
        var areaId = payload.OperationAreaId;
        var clientId = payload.ClientId;

        var from = payload.FromDate.AddDays(TimeAllowance.AttLookbackDays).ToDateTime(TimeOnly.MinValue);
        var to = payload.ToDate.AddDays(TimeAllowance.AttLookforward).ToDateTime(TimeOnly.MinValue);
        var spec = new UserHasViewSpec<Attendance>(canProcess);
        var data = await _uow.Repository
            .Find(spec)
            .AsNoTracking()
            .Include(x => x.Employee)
            .Where(x =>
                x.Status == "Active" &&
                x.WorkDateTime >= from &&
                x.WorkDateTime <= to &&
                x.EmployeeId != null &&
                empIds.Contains(x.EmployeeId.Value)
                //&&
                //(branchId == null || branchId == Guid.Empty || x.BranchId == branchId) && 
                //(departmentId == null || departmentId == Guid.Empty || x.DepartmentId == departmentId) &&
                //(areaId == null || areaId == Guid.Empty || x.OperationAreaId == areaId) &&
                //(payrollGroupId == null || payrollGroupId == Guid.Empty || x.Employee!.PayrollGroupId == payrollGroupId) &&
                //(clientId == null || clientId == Guid.Empty || x.ClientId == clientId)
                )
            .ToListAsync(token);

        var result = data
            .GroupBy(a => new AttendanceEmpId(a.EmployeeId!.Value))
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.WorkDateTime).ToList()
            );
        return result;
    }
    /// <summary>
    /// Removes a log. Each source needs its own permission (AttendanceLogSourcePolicy); removing an
    /// uploaded/synced log first saves its original, for Restore and so a re-sync won't re-add it.
    /// </summary>
    public async Task Remove(Guid Id, AttendanceLogCaller caller, string? reason, CancellationToken token)
    {
        await EnsureWritableAsync(token);
        var attendance = await Context.Attendances.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id, token)
            ?? throw new NotFoundException("Attendance log not found.");

        var rule = AttendanceLogSourcePolicy.EnsureCanDelete(attendance.LogSource, caller);
        if (rule.KeepsOriginal)
            await _originals.RecordDeleteAsync(attendance, reason ?? string.Empty, caller, token);

        await RemoveAsync(Id, token);
        await StagePortalRefreshAsync([(attendance.EmployeeId, attendance.WorkDateTime)], token);
    }

    /// <summary>
    /// Deletes one attendance file upload: every log it imported and every saved original of it,
    /// so the same file can be imported again from scratch. Refused while a saved DTR covers its
    /// logs (UploadBatchDtrLock) -- that DTR has to be deleted first.
    /// </summary>
    public async Task RemoveUploadBatch(string batch, AttendanceLogCaller caller, CancellationToken token)
    {
        await EnsureWritableAsync(token);
        if (string.IsNullOrWhiteSpace(batch))
            throw new ValidationException("Choose an upload to delete.");
        AttendanceLogSourcePolicy.EnsureCanDelete(LOGSOURCE.UPLOADED, caller);
        await UploadBatchDtrLock.EnsureDeletableAsync(Context, batch, token);

        // Confidential employees' logs too: the upload goes as a whole.
        var query = Context.IgnoringConfidentiality<Attendance>()
            .Where(x => x.BatchCode == batch && x.LogSource == LOGSOURCE.UPLOADED);
        // Read before the delete: afterwards nothing says which posted days these punches backed.
        var removed = (await query.AsNoTracking().Select(x => new { x.EmployeeId, x.WorkDateTime }).ToListAsync(token))
            .Select(x => (x.EmployeeId, At: x.WorkDateTime))
            .ToList();

        await query.ExecuteDeleteAsync(token);
        await _originals.RemoveForUploadBatchAsync(batch, token);
        await StagePortalRefreshAsync(removed, token);
    }

    /// <summary>
    /// Attendance file uploads (UPLOADED logs grouped by BatchCode) with a log between the dates,
    /// newest first. An upload whose logs were all removed one by one still shows (through its
    /// saved originals) so it can be deleted, freeing the file to be imported again.
    /// </summary>
    public async Task<List<UploadBatchModel>> GetUploadBatches(UploadBatchFilter filter, CancellationToken token)
    {
        DateTime? from = filter.FromDate?.ToDateTime(TimeOnly.MinValue);
        DateTime? to = filter.ToDate?.ToDateTime(TimeOnly.MinValue).AddDays(1);

        // The uploads touching the dates; each is then counted whole, not just its logs in range.
        var liveCodes = await Context.Attendances.AsNoTracking()
            .Where(x => x.LogSource == LOGSOURCE.UPLOADED && x.BatchCode != "" &&
                        (from == null || x.WorkDateTime >= from) &&
                        (to == null || x.WorkDateTime < to))
            .Select(x => x.BatchCode)
            .Distinct()
            .ToListAsync(token);
        var originalCodes = await Context.AttendanceOriginalLogs.AsNoTracking()
            .Where(x => x.LogSource == LOGSOURCE.UPLOADED && x.BatchCode != "" &&
                        (from == null || x.WorkDateTime >= from) &&
                        (to == null || x.WorkDateTime < to))
            .Select(x => x.BatchCode)
            .Distinct()
            .ToListAsync(token);
        var codes = liveCodes.Union(originalCodes).ToList();
        if (codes.Count == 0) return [];

        var live = await Context.Attendances.AsNoTracking()
            .Where(x => x.LogSource == LOGSOURCE.UPLOADED && codes.Contains(x.BatchCode))
            .GroupBy(x => x.BatchCode)
            .Select(g => new
            {
                BatchCode = g.Key,
                UploadedAt = g.Min(x => x.CreatedAt),
                Remarks = g.Max(x => x.LogRemarks),
                DeviceName = g.Max(x => x.DeviceName),
                LogCount = g.Count(),
                EmployeeCount = g.Where(x => x.EmployeeId != null).Select(x => x.EmployeeId).Distinct().Count(),
                FirstLog = g.Min(x => x.WorkDateTime),
                LastLog = g.Max(x => x.WorkDateTime),
            })
            .ToListAsync(token);

        var changed = await Context.AttendanceOriginalLogs.AsNoTracking()
            .Where(x => x.LogSource == LOGSOURCE.UPLOADED && codes.Contains(x.BatchCode))
            .GroupBy(x => x.BatchCode)
            .Select(g => new
            {
                BatchCode = g.Key,
                Count = g.Count(),
                Remarks = g.Max(x => x.LogRemarks),
                DeviceName = g.Max(x => x.DeviceName),
                FirstLog = g.Min(x => x.WorkDateTime),
                LastLog = g.Max(x => x.WorkDateTime),
            })
            .ToDictionaryAsync(x => x.BatchCode, token);

        var batches = live.Select(x => new UploadBatchModel
        {
            BatchCode = x.BatchCode,
            UploadedAt = PhTime.AsUtc(x.UploadedAt),
            Remarks = x.Remarks ?? string.Empty,
            DeviceName = x.DeviceName ?? string.Empty,
            LogCount = x.LogCount,
            EmployeeCount = x.EmployeeCount,
            FirstLog = x.FirstLog,
            LastLog = x.LastLog,
            ChangedCount = changed.TryGetValue(x.BatchCode, out var c) ? c.Count : 0,
        }).ToList();

        var withLogs = live.Select(x => x.BatchCode).ToHashSet();
        batches.AddRange(changed.Values
            .Where(x => !withLogs.Contains(x.BatchCode))
            .Select(x => new UploadBatchModel
            {
                BatchCode = x.BatchCode,
                Remarks = x.Remarks ?? string.Empty,
                DeviceName = x.DeviceName ?? string.Empty,
                FirstLog = x.FirstLog,
                LastLog = x.LastLog,
                ChangedCount = x.Count,
            }));

        var locks = await UploadBatchDtrLock.FindAsync(Context, codes, token);
        foreach (var batch in batches)
            batch.SavedDtrBatches = locks.TryGetValue(batch.BatchCode, out var dtr) ? dtr : [];

        return batches
            .OrderByDescending(x => x.UploadedAt ?? DateTime.MinValue)
            .ThenByDescending(x => x.LastLog)
            .ToList();
    }
    // Manual entries only (pass-slip logs are manual too) -- uploaded, biometric and synced logs
    // in a batch with the same code are never deleted this way, same rule as Remove(Guid) above.
    public async Task RemoveBatch(string batch, CancellationToken token)
    {
        await EnsureWritableAsync(token);
        var query = Context.Attendances
            .Where(x => x.BatchCode == batch && x.BatchCode != "" && x.LogSource == LOGSOURCE.MANUAL);
        // Read before the delete: afterwards nothing says which posted days these punches backed.
        var removed = (await query.AsNoTracking().Select(x => new { x.EmployeeId, x.WorkDateTime }).ToListAsync(token))
            .Select(x => (x.EmployeeId, At: x.WorkDateTime))
            .ToList();

        await query.ExecuteDeleteAsync(token);
        await StagePortalRefreshAsync(removed, token);
    }
    public async Task Remove(List<Guid> Ids)
    {
        await EnsureWritableAsync(default);
        //only manual logs can be deleted
        await Uow.Context.Attendances
            .Where(x => Ids.Contains(x.Id))
            .ExecuteDeleteAsync();
    }

    public async Task Remove(Attendance model)
    {
        await EnsureWritableAsync(default);
        await RemoveAsync(model);
    }
}
public readonly record struct AttendanceEmpId(Guid EmpId);
public class ManualAttendanceService : BaseService<ManualBatchEntryLog>
{
    public ManualAttendanceService(IUnitOfWorkService service) : base(service)
    {
    }

    public async Task<List<ManualAttModel>> LoadBatch(DateTime? filterDate)
    {
        DateOnly? targetDate = filterDate.HasValue ? DateOnly.FromDateTime(filterDate.Value) : null;
        return await Context.ManualAttendance
            .Where(x => !targetDate.HasValue || x.FromDate == targetDate.Value)
            .Select(x => new ManualAttModel
            {
                BatchCode = x.BatchCode,
                Date = new DateTime(x.FromDate.Year, x.FromDate.Month, x.FromDate.Day)
            })
            .ToListAsync();
    }

}