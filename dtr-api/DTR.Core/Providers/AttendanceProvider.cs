using Hrms.Domain.Entities;

namespace DTR.Core;

public class AttendanceProvider
{
    private readonly GetCurrentAttendancePayload _payload;
    private readonly ICurrentShiftProvider _currentShiftProvider;
    public AttendanceProvider(GetCurrentAttendancePayload payload, ICurrentShiftProvider CurrentShiftProvider)
    {
        _payload = payload;
        _currentShiftProvider = CurrentShiftProvider;
    }


    public List<Attendance> CurrentShiftAttendance()
    {
        var currentShift = _currentShiftProvider.GetCurrentShift();
        var nextShift = _currentShiftProvider.GetNextShift(currentShift);
        return GetAttByShift(currentShift, nextShift);
    }

    private List<Attendance> GetAttByShift(CurrentShift? currentShift, CurrentShift? nextShift)
    {
        if (currentShift == null || nextShift == null)
            return new List<Attendance>();

        var key = new AttendanceEmpId(_payload.Employee.Id);
        if (!_payload.AllEmployeesAttendances.TryGetValue(key, out var attendances) || attendances == null)
            return new List<Attendance>();

        //hours before shift
        var allowance = currentShift.ShiftType == TimeShiftType.FLEXI ? 0 : TimeAllowance.TimeInAllowance;

        //get only att within set window
        var allAtts = attendances
            .Where(x =>
                x.WorkDateTime >= currentShift.StartTime.AddMinutes(allowance) &&
                x.WorkDateTime < nextShift.StartTime.AddMinutes(allowance))
            .ToList();

        //SetFlexiShift(currentShift, allAtts);

        return allAtts;
    }

    private void SetFlexiShift(CurrentShift? currentShift, List<Attendance> attendances)
    {
        //if (currentShift == null) return;
        //if (currentShift.ShiftType == TimeShiftType.FLEXI && attendances.Any())
        //{
        //    //alter shift start and end based on first time in
        //    var att = attendances.FirstOrDefault()!;
        //    var calcEndTime = att.WorkDateTime.AddMinutes(currentShift.MaxWorkingMinutes);
        //    currentShift.StartTime = att.WorkDateTime;
        //    //check if outside bounderies
        //    currentShift.EndTime = calcEndTime <= currentShift.EndTime
        //        ? calcEndTime : currentShift.EndTime;
        //}
    }
}
