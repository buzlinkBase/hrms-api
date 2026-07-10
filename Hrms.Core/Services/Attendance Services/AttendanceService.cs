using Hrms.Domain.Entities;

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

    public async Task<List<AttendanceModel>> GetRawLogs(AttendanceFilterDate filter)
    {
        DateTime fromDate = (filter?.FromDate ?? DateTime.Today).Date;
        DateTime toDate = (filter?.ToDate ?? DateTime.Today).Date.AddDays(1);
        Guid? filterEmployeeId = filter?.EmployeeId;

        return await Uow.Context.Attendances
            .Where(x => x.WorkDateTime >= fromDate &&
                        x.WorkDateTime < toDate &&
                        (filterEmployeeId == null || x.EmployeeId == filterEmployeeId))
            .Select(x => new AttendanceModel
            {
                Id = x.Id,
                Batch = x.BatchCode,
                EmployeeId = x.EmployeeId,
                WorkDateTime = x.WorkDateTime,
                LogSource=x.LogSource.ToString(),
                Name = x.Employee != null
                    ? (x.Employee.LastName ?? "") + ", " + (x.Employee.FirstName ?? "") + " " + (x.Employee.MiddleName ?? "") + " " + (x.Employee.Suffix ?? "")
                    : "",
            })
            .OrderByDescending(x => x.Batch)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }


    public async Task<Dictionary<AttendanceEmpId, List<Attendance>>> LoadAttForDTRProcess(DateOnly from,
        DateOnly to,
        bool canProcess,
        Guid? EmployeeId,
        Guid? departmentId,
        Guid? clientId,
        Guid? payrollGroupId,
        CancellationToken token)
    {
        var dtrLookup = await _uow.Context.DailyTimeRecords
            .Where(dtr => dtr.WorkDate >= from &&
                          dtr.WorkDate <= to)
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
        // 1. Move Date conversion to DateTime to help EF translation
        DateTime fromDate = @from.ToDateTime(TimeOnly.MinValue);
        DateTime toDate = to.ToDateTime(TimeOnly.MaxValue);

        var data = await _uow.Repository
            .Find(spec)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Employee)
            .Where(x =>
                x.WorkDateTime >= fromDate &&
                x.WorkDateTime <= toDate &&
                x.EmployeeId.HasValue &&
                (EmployeeId == null || EmployeeId == Guid.Empty || x.EmployeeId == EmployeeId) &&
                (payrollGroupId == null || payrollGroupId == Guid.Empty || (x.Employee != null && x.Employee.PayrollGroupId == payrollGroupId)) &&
                (clientId == null || clientId == Guid.Empty || x.ClientId == clientId) &&
                (departmentId == null || departmentId == Guid.Empty || (x.Employee != null && x.Employee.DepartmentId == departmentId)))
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