
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace DTR.Core;

public class WorkScheduleResolver
{
    private readonly WorkSchedulePlanService _changeSchedService;
    private readonly TimeShiftService _tsService;
    private readonly EmployeeFixedScheduleService _fixedScheduleService;
    public WorkScheduleResolver(
        WorkSchedulePlanService service,
        TimeShiftService timeShiftService,
        EmployeeFixedScheduleService fixedScheduleService)
    {
        _changeSchedService = service;
        _tsService = timeShiftService;
        _fixedScheduleService = fixedScheduleService;
    }
    public async Task<Dictionary<CurrentTimeShiftKey, CurrentShift>> Resolve(DateOnly fromDate, DateOnly toDate,
        IEnumerable<EmployeeDTRRun> employees,
        CancellationToken token)
    {
        var shifCollection = new Dictionary<CurrentTimeShiftKey, CurrentShift>();
        if (employees == null || !employees.Any())
        {
            return shifCollection;
        }
        var allShifts = await _tsService.FindAllAsync(token);
        var employeesActiveShifts = await _changeSchedService.GetAllCustomShiftsAync(fromDate, toDate, token);
        var employeeFixedSchedules = await _fixedScheduleService.GetAllForEmployeesAsync(
            employees.Select(e => e.Id), token);

        // Priority: Work Rotation Plan (date-specific override) > Fixed Schedule
        // (day-of-week default) > Permanent Shift (employee.TimeShiftId) > Open Shift.
        var schedShift = new OverrideSchedule(employeesActiveShifts, allShifts);
        var fixedScheduleHandler = new FixedScheduleHandler(employeeFixedSchedules, allShifts);
        schedShift.SetNextHandler(fixedScheduleHandler);
        fixedScheduleHandler.SetNextHandler(new FallbackSchedule(allShifts));

        for (DateOnly i = fromDate; i <= toDate; i = i.AddDays(1))
        {
            foreach (var employee in employees)
            {
                var key = new CurrentTimeShiftKey(employee.Id, i);
                var shift = schedShift.Handle(employee, i);
                if (shift != null)
                {
                    shifCollection[key] = shift;
                }
            }
        }
        return shifCollection;
    }
}

public abstract class WorkScheduleHandler
{
    protected WorkScheduleHandler NextHandler { get; set; }
    public void SetNextHandler(WorkScheduleHandler handler)
    {
        NextHandler = handler;
    }
    protected abstract bool IsApplicable(EmployeeDTRRun employee, DateOnly date);
    public CurrentShift Handle(EmployeeDTRRun employee, DateOnly date)
    {
        if (IsApplicable(employee, date))
        {
            return GetCurrentShift(employee, date);
        }
        else if (NextHandler != null)
        {
            return NextHandler.Handle(employee, date);
        }
        return null;
    }
    protected abstract CurrentShift GetCurrentShift(EmployeeDTRRun employee, DateOnly date);
}
public class OverrideSchedule : WorkScheduleHandler
{
    private Dictionary<CurrentTimeShiftKey, WorkSchedulePlan?> _schedules;
    private readonly List<TimeShiftModel> _shifts;

    public OverrideSchedule(Dictionary<CurrentTimeShiftKey, WorkSchedulePlan?> schedules,
        List<TimeShiftModel> shifts)
    {
        _schedules = schedules;
        _shifts = shifts;
    }
    protected override CurrentShift GetCurrentShift(EmployeeDTRRun employee, DateOnly date)
    {
        if (_schedules == null)
        {
            _schedules = new Dictionary<CurrentTimeShiftKey, WorkSchedulePlan?>();
        }

        var key = new CurrentTimeShiftKey(employee.Id, date);
        var result = _schedules.TryGetValue(key, out var values) ? values : null;
        var shiftId = result!.TimeShiftId;
        var shift = _shifts.FirstOrDefault(x => x.Id == shiftId)!;
        var startTime = date.ToDateTime(TimeOnly.FromTimeSpan(shift.StartTime));
        var endTime = date.AddDays(shift.EndTime.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.EndTime - TimeSpan.FromDays(shift.EndTime.Days)));

        return new CurrentShift
        {
            Id = shift.Id,
            ShiftName = shift.ShiftName,
            //Employee = employee,
            ShiftDate = result.PayrollDate,
            StartTime = startTime,
            EndTime = endTime,
            Source = ScheduleSource.Override,
            OverrideId = result.Id,

            //PunchMode = shift.PunchMode,

            GracePeriodMinutes = shift.GracePeriodMinutes,
            LunchBreakDurationMinutes = shift.BreakDurationMinutes,

            LunchBreakOption = shift.WithLunchBreak,
            LunchStartTime = !shift.LunchStartTime.HasValue ? null : date.AddDays(shift.LunchStartTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.LunchStartTime.Value - TimeSpan.FromDays(shift.LunchStartTime.Value.Days))),
            LunchEndTime = !shift.LunchEndTime.HasValue ? null : date.AddDays(shift.LunchEndTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.LunchEndTime.Value - TimeSpan.FromDays(shift.LunchEndTime.Value.Days))),


            WithAMBreak = shift.WithAMBreak,
            AMBreakStartTime = !shift.AMStartTime.HasValue ? null : date.AddDays(shift.AMStartTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.AMStartTime.Value - TimeSpan.FromDays(shift.AMStartTime.Value.Days))),
            AMBreakEndTime = !shift.AMEndTime.HasValue ? null : date.AddDays(shift.AMEndTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.AMEndTime.Value - TimeSpan.FromDays(shift.AMEndTime.Value.Days))),


            WithPMBreakTime = shift.WithPMBreak,
            PMBreakStartTime = !shift.PMStartTime.HasValue ? null : date.AddDays(shift.PMStartTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.PMStartTime.Value - TimeSpan.FromDays(shift.PMStartTime.Value.Days))),
            PMBreakEndTime = !shift.PMEndTime.HasValue ? null : date.AddDays(shift.PMEndTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.PMEndTime.Value - TimeSpan.FromDays(shift.PMEndTime.Value.Days))),


            WithOT = shift.WithOT,
            OTRequireTimeIn = shift.OTRequireTimeIn,
            OTStartTime = shift.OTStart,
            OverTimeThreshold = shift.OverTimeThreshold,
            MaxOvertimeHours = shift.MaxOvertimeHours,
            ShiftType = shift.ShiftType,
            MinimumWorkingMinutes = shift.MinimumWorkMinutes,
            MaxWorkingMinutes = shift.MaxWorkingMinutes,

        };

    }
    protected override bool IsApplicable(EmployeeDTRRun employee, DateOnly date)
    {
        if (_schedules == null || !_schedules.Any()) return false;
        var key = new CurrentTimeShiftKey(employee.Id, date);
        if (_schedules.TryGetValue(key, out var result) && result != null)
        {
            var shiftId = result.TimeShiftId;
            var shift = _shifts.FirstOrDefault(x => x.Id == shiftId);
            return shift != null;
        }
        return false;

    }
}

// Middle tier: the employee's per-day-of-week default (Setup > Employee > Fixed Schedule).
// Applies only when there's no Work Rotation Plan override for that specific date.
public class FixedScheduleHandler : WorkScheduleHandler
{
    private readonly Dictionary<(Guid EmployeeId, DayName Day), EmployeeFixedSchedule> _fixedSchedules;
    private readonly List<TimeShiftModel> _shifts;

    public FixedScheduleHandler(
        Dictionary<(Guid EmployeeId, DayName Day), EmployeeFixedSchedule> fixedSchedules,
        List<TimeShiftModel> shifts)
    {
        _fixedSchedules = fixedSchedules;
        _shifts = shifts;
    }

    private TimeShiftModel? ResolveShift(EmployeeDTRRun employee, DateOnly date)
    {
        var key = (employee.Id, (DayName)date.DayOfWeek);
        if (!_fixedSchedules.TryGetValue(key, out var assigned)) return null;
        return _shifts.FirstOrDefault(x => x.Id == assigned.TimeShiftId);
    }

    protected override bool IsApplicable(EmployeeDTRRun employee, DateOnly date)
    {
        return ResolveShift(employee, date) != null;
    }

    protected override CurrentShift GetCurrentShift(EmployeeDTRRun employee, DateOnly date)
    {
        var shift = ResolveShift(employee, date)!;
        var startTime = date.ToDateTime(TimeOnly.FromTimeSpan(shift.StartTime));
        var endTime = date.AddDays(shift.EndTime.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.EndTime - TimeSpan.FromDays(shift.EndTime.Days)));

        return new CurrentShift
        {
            Id = shift.Id,
            ShiftName = shift.ShiftName,
            ShiftDate = date,
            StartTime = startTime,
            EndTime = endTime,
            Source = ScheduleSource.FixedSchedule,

            GracePeriodMinutes = shift.GracePeriodMinutes,
            LunchBreakDurationMinutes = shift.BreakDurationMinutes,

            LunchBreakOption = shift.WithLunchBreak,
            LunchStartTime = !shift.LunchStartTime.HasValue ? null : date.AddDays(shift.LunchStartTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.LunchStartTime.Value - TimeSpan.FromDays(shift.LunchStartTime.Value.Days))),
            LunchEndTime = !shift.LunchEndTime.HasValue ? null : date.AddDays(shift.LunchEndTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.LunchEndTime.Value - TimeSpan.FromDays(shift.LunchEndTime.Value.Days))),

            WithAMBreak = shift.WithAMBreak,
            AMBreakStartTime = !shift.AMStartTime.HasValue ? null : date.AddDays(shift.AMStartTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.AMStartTime.Value - TimeSpan.FromDays(shift.AMStartTime.Value.Days))),
            AMBreakEndTime = !shift.AMEndTime.HasValue ? null : date.AddDays(shift.AMEndTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.AMEndTime.Value - TimeSpan.FromDays(shift.AMEndTime.Value.Days))),

            WithPMBreakTime = shift.WithPMBreak,
            PMBreakStartTime = !shift.PMStartTime.HasValue ? null : date.AddDays(shift.PMStartTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.PMStartTime.Value - TimeSpan.FromDays(shift.PMStartTime.Value.Days))),
            PMBreakEndTime = !shift.PMEndTime.HasValue ? null : date.AddDays(shift.PMEndTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.PMEndTime.Value - TimeSpan.FromDays(shift.PMEndTime.Value.Days))),

            WithOT = shift.WithOT,
            OTRequireTimeIn = shift.OTRequireTimeIn,
            OTStartTime = shift.OTStart,
            OverTimeThreshold = shift.OverTimeThreshold,
            MaxOvertimeHours = shift.MaxOvertimeHours,
            ShiftType = shift.ShiftType,
            MinimumWorkingMinutes = shift.MinimumWorkMinutes,
            MaxWorkingMinutes = shift.MaxWorkingMinutes,
        };
    }
}

public class FallbackSchedule : WorkScheduleHandler
{
    private readonly List<TimeShiftModel> _allShifts;

    public FallbackSchedule(List<TimeShiftModel> allShifts)
    {
        _allShifts = allShifts;
    }
    private TimeShiftModel getDefaultShift(DateOnly date)
    {
        return new TimeShiftModel
        {
            Id = null,
            ShiftName = "Open Shift",
            StartTime = new TimeSpan(0, 0, 0),
            EndTime = new TimeSpan(1, 0, 0, 0),
            BreakDurationMinutes = 0,
            WithAMBreak = BreakMode.PAID_BREAK,
            WithPMBreak = BreakMode.PAID_BREAK,
            WithLunchBreak = BreakMode.PAID_BREAK,
            LunchStartTime = null,
            LunchEndTime = null,
            AMStartTime = null,
            AMEndTime = null,
            PMStartTime = null,
            PMEndTime = null,
            MinimumWorkMinutes = 0,
            MaxWorkingMinutes = 480,
            WithOT = true,
            ShiftType = TimeShiftType.SPLIT,
            MaxOvertimeHours = null,
            GracePeriodMinutes = 0,
            OTRequireTimeIn = false,
            OverTimeThreshold = 0
        };
    }
    protected override CurrentShift GetCurrentShift(EmployeeDTRRun employee, DateOnly date)
    {
        var shift = _allShifts.FirstOrDefault(x => x.Id == employee.TimeShiftId);
        var source = shift != null ? ScheduleSource.Permanent : ScheduleSource.OpenShift;
        if (shift == null)
        {
            shift = getDefaultShift(date);
        }
        var startTime = date.ToDateTime(TimeOnly.FromTimeSpan(shift.StartTime));
        var endTime = date.AddDays(shift.EndTime.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.EndTime - TimeSpan.FromDays(shift.EndTime.Days)));

        return new CurrentShift
        {
            Id = shift.Id,
            ShiftName = shift.ShiftName,
            //Employee = employee,
            ShiftDate = date,
            StartTime = startTime,
            EndTime = endTime,
            Source = source,

            //PunchMode = shift.PunchMode,

            GracePeriodMinutes = shift.GracePeriodMinutes,
            LunchBreakDurationMinutes = shift.BreakDurationMinutes,

            LunchBreakOption = shift.WithLunchBreak,
            LunchStartTime = !shift.LunchStartTime.HasValue ? null : date.AddDays(shift.LunchStartTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.LunchStartTime.Value - TimeSpan.FromDays(shift.LunchStartTime.Value.Days))),
            LunchEndTime = !shift.LunchEndTime.HasValue ? null : date.AddDays(shift.LunchEndTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.LunchEndTime.Value - TimeSpan.FromDays(shift.LunchEndTime.Value.Days))),


            WithAMBreak = shift.WithAMBreak,
            AMBreakStartTime = !shift.AMStartTime.HasValue ? null : date.AddDays(shift.AMStartTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.AMStartTime.Value - TimeSpan.FromDays(shift.AMStartTime.Value.Days))),
            AMBreakEndTime = !shift.AMEndTime.HasValue ? null : date.AddDays(shift.AMEndTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.AMEndTime.Value - TimeSpan.FromDays(shift.AMEndTime.Value.Days))),


            WithPMBreakTime = shift.WithPMBreak,
            PMBreakStartTime = !shift.PMStartTime.HasValue ? null : date.AddDays(shift.PMStartTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.PMStartTime.Value - TimeSpan.FromDays(shift.PMStartTime.Value.Days))),
            PMBreakEndTime = !shift.PMEndTime.HasValue ? null : date.AddDays(shift.PMEndTime.Value.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.PMEndTime.Value - TimeSpan.FromDays(shift.PMEndTime.Value.Days))),


            WithOT = shift.WithOT,
            OTRequireTimeIn = shift.OTRequireTimeIn,
            OTStartTime = shift.OTStart,
            OverTimeThreshold = shift.OverTimeThreshold,
            MaxOvertimeHours = shift.MaxOvertimeHours,
            ShiftType = shift.ShiftType,
            MinimumWorkingMinutes = shift.MinimumWorkMinutes,
            MaxWorkingMinutes = shift.MaxWorkingMinutes,

        };
    }
    protected override bool IsApplicable(EmployeeDTRRun employee, DateOnly date)
    {
        return true;
    }
}

