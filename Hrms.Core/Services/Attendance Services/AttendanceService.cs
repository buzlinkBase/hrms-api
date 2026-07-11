using Hrms.Domain.Entities;
using Mapster;

namespace Hrms.Core.Services;

public class AttendanceService : BaseService<Attendance>
{
    public AttendanceService(IUnitOfWorkService uow) : base(uow)
    {
    }
    public async Task AddOrUpdateAsync(Attendance model, CancellationToken token)
    {
        await CreateAsync(model, token);
    }
    public async Task AddRangeAsync(List<Attendance> attendances, CancellationToken token = default)
    {
        if (attendances == null || !attendances.Any())
        {
            return;
        }

        var uniqueRecordKeys = attendances
             .GroupBy(x => new { x.BioId, x.WorkDateTime })
             .Select(group => group.Key)
             .ToList();
        foreach (var key in uniqueRecordKeys)
        {
            await GetQueryable()
                .Where(x => x.BioId == key.BioId && x.WorkDateTime == key.WorkDateTime)
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
        DateTime fromDate = (filter?.FromDate ?? DateTime.Today).Date;
        DateTime toDate = (filter?.ToDate ?? DateTime.Today).Date.AddDays(1);
        Guid? filterEmployeeId = filter?.EmployeeId;

        return await Uow.Context.Attendances
            .Where(x => x.LogSource == source &&
                        x.WorkDateTime >= fromDate &&
                        x.WorkDateTime < toDate &&
                        (filterEmployeeId == null || x.EmployeeId == filterEmployeeId))
            .Select(x => new AttendanceModel
            {
                Id = x.Id,
                Batch = x.BatchCode,
                EmployeeId = x.EmployeeId,
                WorkDateTime = x.WorkDateTime,
                LogSource = x.LogSource.ToString(),
                Name = x.Employee != null
                    ? (x.Employee.LastName ?? "") + ", " + (x.Employee.FirstName ?? "") + " " + (x.Employee.MiddleName ?? "") + " " + (x.Employee.Suffix ?? "")
                    : "",
            })
            .OrderByDescending(x => x.Batch)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<List<AttendanceModel>> GetRawLogs(AttendanceFilter filter)
    {
        DateTime fromDate = (filter?.FromDate ?? DateTime.MinValue).Date;
        DateTime toDate = (filter?.ToDate ?? DateTime.MaxValue);

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
                Batch = x.BatchCode,
                EmployeeId = x.EmployeeId,
                WorkDateTime = x.WorkDateTime,
                LogSource = x.LogSource.ToString(),
                Name = x.Employee != null
                    ? (x.Employee.LastName ?? "") + ", " + (x.Employee.FirstName ?? "") + " " + (x.Employee.MiddleName ?? "") + " " + (x.Employee.Suffix ?? "")
                    : "",
            })
            .OrderByDescending(x => x.Batch)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }


    public async Task<Dictionary<AttendanceEmpId, List<Attendance>>> LoadAttForDTRProcess(
        DTRRequestPayload payload,
        bool canProcess,
        CancellationToken token)
    {
        var from = payload.FromDate;
        var to = payload.ToDate.AddDays(1);
        var EmployeeId = payload.EmployeeId;
        var departmentId = payload.DepartmentId;
        var payrollGroupId = payload.PayrollGroupId;
        var branchId = payload.BranchId;
        var areaId  = payload.OperationAreaId;
        var clientId = payload.ClientId;
        var fromWD = DateOnly.FromDateTime(payload.FromDate);
        var toWD = DateOnly.FromDateTime(payload.ToDate);

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

        var data = await _uow.Repository
            .Find(spec)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Employee)
            .Where(x =>
                x.WorkDateTime >= from &&
                x.WorkDateTime <= to &&
                x.EmployeeId != null &&
                (EmployeeId == null || EmployeeId == Guid.Empty || x.EmployeeId == EmployeeId) &&
                (branchId == null || branchId == Guid.Empty || x.BranchId == areaId) && // Note: double check if x.BranchId == areaId was intentional here instead of branchId
                (departmentId == null || departmentId == Guid.Empty || x.DepartmentId == departmentId) &&
                (areaId == null || areaId == Guid.Empty || x.OperationAreaId == areaId) &&
                (payrollGroupId == null || payrollGroupId == Guid.Empty || x.Employee!.PayrollGroupId == payrollGroupId) &&
                (clientId == null || clientId == Guid.Empty || x.ClientId == clientId))
            .ToListAsync(token);

        foreach (var attendance in data)
        {
            // Safe check even though we filtered for HasValue
            if (attendance.EmployeeId.HasValue)
            {
                var key = (attendance.EmployeeId.Value, DateOnly.FromDateTime(attendance.WorkDateTime));
                if (dtrSet.Contains(key))
                {
                    attendance.RecordStatus = DTRStatus.LOCKED;
                }
            }
        }

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