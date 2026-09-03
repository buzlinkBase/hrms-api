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
            .ExecuteUpdateAsync(x => x.SetProperty(xx => xx.WorkDateTime, model.WorkTime), token)
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
        await Context.Attendances
         .Where(x => x.BioId.HasValue && x.BioId == employee.BioId)
         .ExecuteUpdateAsync(s => s
             .SetProperty(e => e.EmployeeId, employee.Id)
             .SetProperty(e => e.ClientId, e => e.ClientId ?? employee.ClientId)
             .SetProperty(e => e.BranchId, e => e.BranchId ?? employee.BranchId)
             .SetProperty(e => e.ClientId, e => e.ClientId ?? employee.ClientId)
             .SetProperty(e => e.OperationAreaId, e => e.OperationAreaId ?? employee.AreaId)
             .SetProperty(e => e.DepartmentId, e => e.DepartmentId ?? employee.DepartmentId),
             cancellationToken: token);

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

    public async Task<List<AttendanceModel>> GetRawLogs(AttendanceFilter filter)
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
            .Where(x => x.WorkDateTime >= fromDate && x.WorkDateTime < toDate &&
                        (EmployeeId == null || x.EmployeeId == EmployeeId) &&
                        (BranchId == null || x.BranchId == BranchId) &&
                        (DepartmentId == null || x.DepartmentId == DepartmentId) &&
                        (PayrollGroupId == null || x.DepartmentId == PayrollGroupId) &&
                        (OperationAreaId == null || x.EmployeeId == OperationAreaId))
            .Select(x => new AttendanceModel
            {
                Id = x.Id,
                BioId = x.BioId,
                Batch = x.BatchCode,
                EmployeeId = x.EmployeeId,
                WorkDateTime = x.WorkDateTime,
                LogSource = x.LogSource.ToString(),
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

        var fromWD = payload.FromDate;
        var toWD = payload.ToDate;
        var from = payload.FromDate.AddDays(TimeAllowance.AttLookbackDays).ToDateTime(TimeOnly.MinValue);
        var to = payload.ToDate.AddDays(TimeAllowance.AttLookforward).ToDateTime(TimeOnly.MinValue);

        var dtrLookup = await _uow.Context.DailyTimeRecords
            .Where(dtr => dtr.WorkDate >= fromWD &&
                          dtr.WorkDate <= toWD)
            .Select(dtr => new
            {
                dtr.EmployeeId,
                dtr.WorkDate
            })
            .ToListAsync(token);

        var dtrSet = new HashSet<(Guid EmployeeId, DateOnly WorkDate)>(
            dtrLookup.Select(d => (d.EmployeeId, d.WorkDate))
        );
        var spec = new UserHasViewSpec<Attendance>(canProcess);

        //var EmployeeId = payload.EmployeeId;
        var data = await _uow.Repository
            .Find(spec)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Employee)
            .Where(x =>
                x.Status == "Active" &&
                x.WorkDateTime >= from &&
                x.WorkDateTime <= to &&
                x.EmployeeId != null &&
                empIds.Contains(x.EmployeeId.Value) &&
                (branchId == null || branchId == Guid.Empty || x.BranchId == areaId) && // Note: double check if x.BranchId == areaId was intentional here instead of branchId
                (departmentId == null || departmentId == Guid.Empty || x.DepartmentId == departmentId) &&
                (areaId == null || areaId == Guid.Empty || x.OperationAreaId == areaId) &&
                (payrollGroupId == null || payrollGroupId == Guid.Empty || x.Employee!.PayrollGroupId == payrollGroupId) &&
                (clientId == null || clientId == Guid.Empty || x.ClientId == clientId))
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
    public async Task RemoveBatch(string batch, CancellationToken token)
    {
        await Context.Attendances
             .Where(x => x.BatchCode == batch && x.BatchCode != "")
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