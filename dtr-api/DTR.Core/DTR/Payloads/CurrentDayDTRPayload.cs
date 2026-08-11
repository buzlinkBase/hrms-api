

using Hrms.Domain.Entities;
using NPOI.SS.Formula.Functions;

namespace DTR.Core;

public class CurrentDayDTRPayload
{
    public static DTRProcessorPayload SetPayload(
    DTRContextModel context,
    EmployeeDTRRun curEmployee,
    DateOnly currentDate,
    bool processOnlyPairedAtt = true)
    {
        var shiftProvider = CreateShiftProvider(context, curEmployee, currentDate);

        var currentShift = shiftProvider.GetCurrentShift();
        //capture attendance
        var attendanceProvider = CreateAttendanceProvider(context, curEmployee, shiftProvider);
        var currentAtt = attendanceProvider.CurrentShiftAttendance();
        var attendance = processOnlyPairedAtt
            ? AttendancePairGrabber.GetPairs(currentAtt)
            : currentAtt;

        var leaveProvider = new LeaveApplicationProvider(context.Leaves, curEmployee);
        var travelProvider = new TravelApplicationProvider(context.Travels, curEmployee);
        var currentLeave = leaveProvider.GetApplication(currentDate);
        var currentTravel = travelProvider.GetApplication(currentDate);

        //load travel attendance
        attendance.AddRange(SetTravelAttendance(currentTravel, curEmployee));
        attendance = attendance.OrderByDescending(p => p.WorkDateTime).ToList();

        var payload = new DTRProcessorPayloadBuilder()
            .SetEmployee(curEmployee)
            .SetCurrentDate(currentDate)
            .SetCurrentShift(currentShift)
            .SetCurrentShiftProvider(shiftProvider)
            .SetAttendanceProvider(attendanceProvider)
            .SetCurrentAtt(attendance)
            .SetLeaveProvider(leaveProvider)
            .SetTravelProvider(travelProvider)
            .SetCurrentLeave(currentLeave)
            .SetCurrentTravel(currentTravel)
            .SetOTProvider(new OverTimeServiceProvider(context.OverTimeApplications, curEmployee))
            .SetUTProvider(new UnderTimeServiceProvider(context.UnderTimeApplications, curEmployee))
            .SetHolidayProvider(HolidayProviderFactory.Create(curEmployee, context.Holidays))
            .SetClientPolicyProvider(new ClientPolicyProvider(context.ClientPolicies))
            .SetCompanyPolicy(context.CompanyPolicy)
            .SetEmployeePolicy(context.EmployeePolicies.TryGetValue(new EmployeePolicyKey(curEmployee.Id), out var policy) ? policy : null)
            .SetDayOff(context.DayOffs)
            .SetDTRContext(context)
            .Build();

        payload.Provider.DtrContextModel = context;
        return payload;

    }

    private static List<Attendance> SetTravelAttendance(TravelOrderApplication? currentTravel,
        EmployeeDTRRun curEmployee)
    {
        var atts = new List<Attendance>();
        if (currentTravel != null &&
            !currentTravel.IsManualEntry &&
             currentTravel.StartTime.HasValue &&
             currentTravel.EndTime.HasValue)
        {
            atts.Add(
                new Attendance
                {
                    BioId = curEmployee.BioId,
                    BranchId = curEmployee.BranchId,
                    ClientId = curEmployee.ClientId,
                    DepartmentId = curEmployee.DepartmentId,
                    OperationAreaId = curEmployee.AreaId,
                    EmployeeId = curEmployee.Id,
                    WorkDateTime = currentTravel.StartTime.Value,
                });
            atts.Add(
               new Attendance
               {
                   BioId = curEmployee.BioId,
                   BranchId = curEmployee.BranchId,
                   ClientId = curEmployee.ClientId,
                   DepartmentId = curEmployee.DepartmentId,
                   OperationAreaId = curEmployee.AreaId,
                   EmployeeId = curEmployee.Id,
                   WorkDateTime = currentTravel.EndTime.Value,
               });
        }
        return atts;
    }
    public static ICurrentShiftProvider CreateShiftProvider(DTRContextModel context, EmployeeDTRRun employee, DateOnly curDate)
    {
        return ShiftProviderFactory.Create(context, employee, curDate);
    }

    private static AttendanceProvider CreateAttendanceProvider(
        DTRContextModel context,
        EmployeeDTRRun employee,
        ICurrentShiftProvider shiftProvider)
    {
        var payload = new GetCurrentAttendancePayload(context.CleanAttendance, employee);
        return new AttendanceProvider(payload, shiftProvider);
    }
}

public class ShiftProviderFactory
{
    public static ICurrentShiftProvider Create(DTRContextModel context, EmployeeDTRRun employee, DateOnly curDate)
    {
        //read settings config to use here
        var payload = new CurrentShiftProviderPayload(context.AllShifts, employee, curDate);
        return new CurrentShiftProvider(context, payload);
        //return new CrossMultiDateCurrentShiftProvider(context, payload);
    }
}