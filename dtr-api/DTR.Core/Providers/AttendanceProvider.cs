using Hrms.Domain.Entities;

namespace DTR.Core;

public class AttendanceProvider
{
    private readonly GetCurrentAttendancePayload _payload;
    private readonly ICurrentShiftProvider _currentShiftProvider;
    public AttendanceProvider(GetCurrentAttendancePayload payload, 

        ICurrentShiftProvider CurrentShiftProvider)
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
        if (!_payload.Context.CleanAttendance.TryGetValue(key, out var attendances) || attendances == null)
            return new List<Attendance>();

        var allowance = currentShift.ShiftType == TimeShiftType.SPLIT ? 0 : _payload.Context.CompanyPolicy.TimeInAllowance;
        var allAtts = attendances
            .Where(x =>
                x.WorkDateTime >= currentShift.StartTime.AddMinutes(allowance) &&
                x.WorkDateTime < nextShift.StartTime.AddMinutes(allowance))
            .ToList();
        return allAtts;
    } 
}
