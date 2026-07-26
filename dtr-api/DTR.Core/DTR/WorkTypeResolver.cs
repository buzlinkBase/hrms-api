using Hrms.Domain.Entities;

namespace DTR.Core;

/// <summary>
/// Resolves the <see cref="WorkType"/> for a shift by evaluating attendance, leave,
/// rest-day, and holiday status against a fixed priority order:
///
///   1. Incomplete attendance         — always wins; nothing else matters if the punches don't pair up.
///   2. Worked while on leave         — half-day leave: attendance plus an active leave
///                                       application for the same day.
///   3. Worked the shift (pure duty)  — rest day / legal / special non-working holiday duty variants,
///                                       else regular work (including special working holidays).
///   4. On leave (no duty)            — leave on a rest day collapses to a plain rest day;
///                                       otherwise paid/unpaid leave per the leave's pay type.
///   5. Rest day (no duty, no leave).
///   6. Holiday (no duty, no leave, not a rest day) — legal, special non-working, or absent on special working.
///   7. Absent — fallback when none of the above apply.
/// </summary>
public static class WorkTypeResolver
{
    public static WorkType Resolve(TimeContext context)
    {
        var attendance = context.Payload.Provider.AttendanceProvider.CurrentShiftAttendance();
        var hasAttendance = attendance.Any();
        var hasIncompleteAttendance = hasAttendance && attendance.Count % 2 != 0;
        if (hasIncompleteAttendance)
            return WorkType.Incomplete;

        var leave = context.Payload.Data.CurrentLeave;
        var isRestDay = new IsRestDaySpec().IsSatisfiedBy(context.CanonicalTimeRange, context);
        var isLegalHoliday = context.IsLegalHoliday();
        var isSpecialWorking = context.IsSpecialWorking();
        var isSpecialNonWorking = context.IsSpecialNonWorking();

        if (hasAttendance)
            return ResolveDutyWorkType(leave, isRestDay, isLegalHoliday, isSpecialWorking, isSpecialNonWorking);

        if (leave != null)
            return ResolveLeaveWorkType(leave, isRestDay);

        if (isRestDay)
        {
            if (isLegalHoliday) return WorkType.RestDayLegalHoliday;
            if (isSpecialNonWorking) return WorkType.RestDaySpecialHoliday;
            return WorkType.RestDay;
        }

        if (isLegalHoliday)
            return WorkType.LegalHoliday;

        if (isSpecialNonWorking)
            return WorkType.SpecialNonWorkingHoliday;

        if (isSpecialWorking)
            return WorkType.Absent;//return WorkType.SpecialWorkingHoliday;


        return WorkType.Absent;
    }

    /// <summary>
    /// Employee has attendance for the shift. If a leave application is active (half-day leave),
    /// that combined classification wins. Otherwise resolves pure duty.
    /// </summary>
    private static WorkType ResolveDutyWorkType(
        LeaveApplication? leave,
        bool isRestDay,
        bool isLegalHoliday,
        bool isSpecialWorking,
        bool isSpecialNonWorking)
    {
        if (leave != null)
            return ResolveDutyOnLeaveWorkType(leave);

        if (isRestDay)
        {
            if (isLegalHoliday) return WorkType.RestDayLegalHolidayDuty;
            if (isSpecialNonWorking) return WorkType.RestDaySpecialHolidayDuty;
            return WorkType.RestDayDuty;
        }

        if (isLegalHoliday)
            return WorkType.LegalHolidayDuty;

        if (isSpecialNonWorking)
            return WorkType.SpecialHolidayDuty;

        if (isSpecialWorking)
            return WorkType.SpecialWorkingHoliday;

        return WorkType.RegularWorkDay;
    }

    /// <summary>
    /// Employee worked part of the shift while having an active leave application.
    /// </summary>
    private static WorkType ResolveDutyOnLeaveWorkType(LeaveApplication leave) =>
        leave.PayType == PayType.WithPay
            ? WorkType.PaidLeave
            : WorkType.UnpaidLeave;

    /// <summary>
    /// Employee has no attendance but is on leave. Leave on a rest day collapses
    /// to a plain rest day.
    /// </summary>
    private static WorkType ResolveLeaveWorkType(LeaveApplication leave, bool isRestDay)
    {
        if (isRestDay)
            return WorkType.RestDay;

        return leave.PayType == PayType.WithPay
            ? WorkType.PaidLeave
            : WorkType.UnpaidLeave;
    }
}