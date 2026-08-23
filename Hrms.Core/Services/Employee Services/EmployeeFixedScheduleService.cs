using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

public class EmployeeFixedScheduleService : BaseService<EmployeeFixedSchedule>
{
    public EmployeeFixedScheduleService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<List<EmployeeFixedScheduleModel>> GetByEmployeeAsync(Guid employeeId, CancellationToken token)
    {
        return await GetQueryable()
            .Where(x => x.EmployeeId == employeeId)
            .Select(x => new EmployeeFixedScheduleModel
            {
                Id = x.Id,
                EmployeeId = x.EmployeeId,
                DayName = x.DayName,
                TimeShiftId = x.TimeShiftId,
                TimeShiftName = x.TimeShift!.ShiftName,
                ShiftType = x.TimeShift!.ShiftType,
                StartTime = x.TimeShift!.StartTime,
                EndTime = x.TimeShift!.EndTime,
            })
            .ToListAsync(token);
    }

    public async Task<EmployeeFixedScheduleModel> SetDayAsync(Guid employeeId, DayName dayName, Guid timeShiftId, CancellationToken token)
    {
        var timeShift = await _uow.Repository
            .Find<Hrms.Domain.Entities.TimeShift>(x => x.Id == timeShiftId)
            .AsNoTracking()
            .FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Time shift not found");

        var existing = await GetQueryable()
            .Where(x => x.EmployeeId == employeeId && x.DayName == dayName)
            .FirstOrDefaultAsync(token);

        if (existing == null)
        {
            existing = new EmployeeFixedSchedule
            {
                Id = Guid.CreateVersion7(),
                EmployeeId = employeeId,
                DayName = dayName,
                TimeShiftId = timeShiftId,
            };
            await CreateAsync(existing, token);
        }
        else
        {
            existing.TimeShiftId = timeShiftId;
            await ModifyAsync(existing, token);
        }
        await CommitChangesAsync(token);

        return new EmployeeFixedScheduleModel
        {
            Id = existing.Id,
            EmployeeId = existing.EmployeeId,
            DayName = existing.DayName,
            TimeShiftId = timeShift.Id,
            TimeShiftName = timeShift.ShiftName,
            ShiftType = timeShift.ShiftType,
            StartTime = timeShift.StartTime,
            EndTime = timeShift.EndTime,
        };
    }

    public async Task UnassignDayAsync(Guid employeeId, DayName dayName, CancellationToken token)
    {
        var existing = await GetQueryable()
            .Where(x => x.EmployeeId == employeeId && x.DayName == dayName)
            .FirstOrDefaultAsync(token);

        if (existing == null) return;

        await RemoveAsync(existing.Id, token);
        await CommitChangesAsync(token);
    }

    // Used by WorkScheduleResolver (DTR.Core) to resolve the day-of-week fallback tier —
    // Work Rotation Plan > Fixed Schedule (this) > Permanent Shift > Open Shift.
    public async Task<Dictionary<(Guid EmployeeId, DayName Day), EmployeeFixedSchedule>> GetAllForEmployeesAsync(
        IEnumerable<Guid> employeeIds, CancellationToken token)
    {
        var ids = employeeIds.ToHashSet();
        var rows = await GetQueryable()
            .AsNoTracking()
            .Where(x => ids.Contains(x.EmployeeId))
            .ToListAsync(token);

        return rows.ToDictionary(x => (x.EmployeeId, x.DayName));
    }
}
