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
        if (!attendances.Any()) return;
        var recordKeys = attendances
             .Select(x => new { x.BioId, x.WorkDateTime })
             .Distinct()
             .ToList();

        var bioIds = recordKeys.Select(k => k.BioId).Distinct().ToList();
        var workDates = recordKeys.Select(k => k.WorkDateTime).Distinct().ToList();

        var allExisting = _uow.Repository
            .Find<Attendance>(x =>
                bioIds.Contains(x.BioId) &&
                workDates.Contains(x.WorkDateTime));

        await allExisting.ExecuteDeleteAsync();
        foreach (var attendance in attendances)
        {
            attendance.Id = Guid.Empty;
            //attendance.UserId =   _currentUser.Id;
            attendance.UserName = "";
        }
        await base.CreateRangeAsync(attendances, token);
    }
    public async Task AddRange(List<Attendance> models, CancellationToken token)
    {
        await CreateRangeAsync(models, token);
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
        // Build DTR lookup based on EmployeeId and Date

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
        var data = await _uow.Repository
            .Find(spec)
            .AsNoTracking()
            .AsSingleQuery()
            .Where(x =>
                DateOnly.FromDateTime(x.WorkDateTime) >= from &&
                DateOnly.FromDateTime(x.WorkDateTime) <= to &&
                (EmployeeId == null || EmployeeId == Guid.Empty || x.EmployeeId == EmployeeId) &&
                (payrollGroupId == null || payrollGroupId == Guid.Empty || x.Employee.PayrollGroupId == payrollGroupId) &&
                (clientId == null || clientId == Guid.Empty || x.ClientId == clientId) &&
                (departmentId == null || departmentId == Guid.Empty || x.Employee.DepartmentId == departmentId))
            .Include(x => x.Employee)
            .ToListAsync(token);

        foreach (var attendance in data)
        {
            var key = (attendance.EmployeeId, DateOnly.FromDateTime(attendance.WorkDateTime));
            if (dtrSet.Contains(key))
                attendance.RecordStatus = DTRStatus.LOCKED;
        }

        var result = data
            .GroupBy(a => new AttendanceEmpId(a.EmployeeId))
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.WorkDateTime).ToList()
            );
        return result;
    }

    public async Task Remove(Guid Id)
    {
        await RemoveAsync(Id);
    }
    public async Task Remove(Attendance model)
    {
        await RemoveAsync(model);
    }
}
public readonly record struct AttendanceEmpId(Guid EmpId);
