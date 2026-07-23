
using Hrms.Domain.Entities;

namespace DTR.Core;

public class WorkScheduleResolver
{
    private readonly WorkSchedulePlanService _changeSchedService;
    private readonly TimeShiftService _tsService;
    public WorkScheduleResolver(
        WorkSchedulePlanService service,
        TimeShiftService timeShiftService)
    {
        _changeSchedService = service;
        _tsService = timeShiftService;
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

        var schedShift = new OverrideSchedule(employeesActiveShifts, allShifts);
        schedShift.SetNextHandler(new FallbackSchedule(allShifts));

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


//handlers
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
            ShiftName = shift.ShiftName,
            //Employee = employee,
            ShiftDate = result.PayrollDate,
            StartTime = startTime,
            EndTime = endTime,

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
        };
    }
    protected override CurrentShift GetCurrentShift(EmployeeDTRRun employee, DateOnly date)
    {
        var shift = _allShifts.FirstOrDefault(x => x.Id == employee.TimeShiftId);
        if (shift == null)
        {
            shift = getDefaultShift(date);
        }
        var startTime = date.ToDateTime(TimeOnly.FromTimeSpan(shift.StartTime));
        var endTime = date.AddDays(shift.EndTime.Days).ToDateTime(TimeOnly.FromTimeSpan(shift.EndTime - TimeSpan.FromDays(shift.EndTime.Days)));

        return new CurrentShift
        {
            ShiftName = shift.ShiftName,
            //Employee = employee,
            ShiftDate = date,
            StartTime = startTime,
            EndTime = endTime,

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

