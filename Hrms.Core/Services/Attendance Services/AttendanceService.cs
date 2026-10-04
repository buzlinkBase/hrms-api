using Castle.Components.DictionaryAdapter.Xml;
using DTR.Core;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Polly;

namespace Hrms.Core.Services;

public class AttendanceService : BaseService<Attendance>
{
    public AttendanceService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task Update(UpdateAttendance model, CancellationToken token)
    {
        await Context.Attendances
            .Where(x => x.Id == model.Id)
            .ExecuteUpdateAsync(x => x
                .SetProperty(xx => xx.WorkDateTime, model.WorkTime)
                .SetProperty(xx => xx.EditRemarks, model.Remarks), token)
            ;
    }

    public async Task AddRangeAsync(List<Attendance> attendances, CancellationToken token = default)
    {
        if (attendances == null || !attendances.Any())
        {
            return;
        }

        var uniqueRecordKeys = attendances
             .GroupBy(x => new { x.BioId, x.EmployeeId, x.WorkDateTime })
             .Select(group => group.Key)
             .ToList();
        foreach (var key in uniqueRecordKeys)
        {
            await GetQueryable()
                .Where(x => x.BioId == key.BioId && x.EmployeeId == key.EmployeeId && x.WorkDateTime == key.WorkDateTime)
                .ExecuteDeleteAsync(token);
        }

        // 4. Prepare the incoming records for clean insertions
        foreach (var attendance in attendances)
        {
            // Resetting the ID allows the database to generate a fresh, non-conflicting primary key
            attendance.Id = Guid.Empty;
            attendance.UserName = string.Empty;
            //attendance.UserId = _currentUser.Id;
        }
        await CreateRangeAsync(attendances, token);
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

    public async Task<List<AttendanceModel>> GetAllLogsInRange(Guid employeeId, CurrentShiftInfo current, CurrentShiftInfo next, CancellationToken ct)
    {
        var settings = await Context.GeneralSettings.FirstOrDefaultAsync(x => x.Description == nameof(SettingKey.TimeInAllowance), ct);
        var allowance = current.ShiftType == TimeShiftType.SPLIT ? 0 : GeneralSettingsUtil.ParseInt(settings?.Value, -120);
        Guid? filterEmployeeId = employeeId;

        var startTime = current.StartTime!.Value;
        var endTime = next.StartTime!.Value.AddMinutes(allowance);

        return await Uow.Context.Attendances
            .Where(x => x.WorkDateTime >= startTime &&
                        x.WorkDateTime <= endTime &&
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
    public async Task Remove(Guid Id, CancellationToken token)
    {
        var attendance = await Context.Attendances.FirstOrDefaultAsync(x => x.Id == Id, token);
        if (attendance == null)
            throw new NotFoundException("Attendance log not found.");
        if (attendance.LogSource != LOGSOURCE.MANUAL)
            throw new ValidationException("Only manually entered attendance logs can be deleted.");

        await RemoveAsync(Id, token);

    }
    // Manual entries only (pass-slip logs are manual too) -- uploaded, biometric and synced logs
    // in a batch with the same code are never deleted this way, same rule as Remove(Guid) above.
    public async Task RemoveBatch(string batch, CancellationToken token)
    {
        await Context.Attendances
             .Where(x => x.BatchCode == batch && x.BatchCode != "" && x.LogSource == LOGSOURCE.MANUAL)
             .ExecuteDeleteAsync(token);
    }
    public async Task Remove(List<Guid> Ids)
    {
        //only manual logs can be deleted
        await Uow.Context.Attendances
            .Where(x => Ids.Contains(x.Id))
            .ExecuteDeleteAsync();
    }

    public async Task Remove(Attendance model)
    {
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