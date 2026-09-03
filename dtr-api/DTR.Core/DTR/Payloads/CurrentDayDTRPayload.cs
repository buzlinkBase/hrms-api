

using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Components.Web;

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
        var attendanceProvider = CreateAttendanceProvider(context, curEmployee, shiftProvider);
        var currentAtt = attendanceProvider.CurrentShiftAttendance();
        var attendance = processOnlyPairedAtt
            ? AttendancePairGrabber.GetPairs(currentAtt)
            : currentAtt;

        var leaveProvider = new LeaveApplicationProvider(context.Leaves, curEmployee);
        var travelProvider = new TravelApplicationProvider(context.Travels, curEmployee);
        var currentLeaves = leaveProvider.GetApplications(currentDate);
        var currentTravel = travelProvider.GetApplication(currentDate);
        attendance.AddRange(SetTravelAttendance(currentTravel, curEmployee));
        foreach (var currentLeave in currentLeaves)
        {
            attendance.AddRange(VirtualTimeComposer.SetLeaveAttendance(currentLeave, curEmployee, currentShift));
        }
        attendance = attendance.OrderBy(p => p.WorkDateTime).ToList();

        var payload = new DTRProcessorPayloadBuilder()
            .SetEmployee(curEmployee)
            .SetCurrentDate(currentDate)
            .SetCurrentShift(currentShift)
            .SetCurrentShiftProvider(shiftProvider)
            .SetAttendanceProvider(attendanceProvider)
            .SetCurrentAtt(attendance)
            .SetLeaveProvider(leaveProvider)
            .SetTravelProvider(travelProvider)
            .SetCurrentLeave(currentLeaves)
            .SetCurrentTravel(currentTravel)
            .SetOTProvider(new OverTimeServiceProvider(context.OverTimeApplications, curEmployee))
            .SetUTProvider(new UnderTimeServiceProvider(context.UnderTimeApplications, curEmployee))
            .SetHolidayProvider(HolidayProviderFactory.Create(curEmployee, context.Holidays))
            .SetCompanyPolicy(context.CompanyPolicy)
            .SetClientPolicyProvider(new ClientPolicyProvider(context.ClientPolicies))
            .SetEmployeePolicy(context.EmployeePolicies.TryGetValue(new EmployeePolicyKey(curEmployee.Id), out var policy) ? policy : null)
            .SetDayOff(context.DayOffs)
            .SetDTRContext(context)
            .Build();

        payload.Provider.DtrContextModel = context;
        return payload;
    }
    private static List<Attendance> SetTravelAttendance(TravelOrderApplication? currentTravel, EmployeeDTRRun curEmployee)
    {
        if (currentTravel == null || currentTravel.IsManualEntry
            || !currentTravel.StartTime.HasValue || !currentTravel.EndTime.HasValue)
            return [];
        return VirtualAttendanceFactory.CreatePair(curEmployee, currentTravel.StartTime.Value, currentTravel.EndTime.Value);
    }

    // Virtual attendance is injected only when the employer bears the cost.
    // Government-only leave (maternity, paternity, etc.) is paid by SSS/PhilHealth —
    // the company does not inject a paid time block.
    public static ICurrentShiftProvider CreateShiftProvider(DTRContextModel context, EmployeeDTRRun employee, DateOnly curDate)
    {
        return ShiftProviderFactory.Create(context, employee, curDate);
    }
    private static AttendanceProvider CreateAttendanceProvider(
        DTRContextModel context,
        EmployeeDTRRun employee,
        ICurrentShiftProvider shiftProvider)
    {
        var payload = new GetCurrentAttendancePayload(context, employee);
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

public static class VirtualTimeComposer
{
    public static List<Attendance> SetLeaveAttendance(
        LeaveApplication? curLeave,
        EmployeeDTRRun curEmployee,
        CurrentShift? shift,
        bool WithPayOnly = true)
    {
        if (curLeave == null || (WithPayOnly && !IsEligibleForVirtualAttendance(curLeave)))
        {
            return [];
        }
        var strategy = LeaveAttendanceStrategyFactory.Create(curLeave.DurationType);
        return strategy.CreateVirtualAttendance(curLeave, curEmployee, shift);
    }
    // OneTime-payout leave (e.g. a Shared-funded SSS maternity lump sum) is paid entirely
    // through PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross instead of the normal
    // DTR/attendance channel — injecting a full "worked a shift" virtual attendance block for
    // these days would additionally feed RegularDayPay as if the employee physically worked,
    // double-paying on top of the lump sum. WorkType resolution for the day (PaidLeave/
    // GovFundedLeave, never Absent) does not depend on virtual attendance existing, so
    // excluding OneTime here doesn't misclassify the day.
    public static bool IsEligibleForVirtualAttendance(LeaveApplication leave) =>
    leave.PayType != PayType.WithoutPay &&
    leave.PayoutMode != PayoutMode.OneTime &&
    leave.Leave?.PaySource is PaySource.Company or PaySource.Shared;
}