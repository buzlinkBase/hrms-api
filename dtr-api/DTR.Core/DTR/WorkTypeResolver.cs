using Hrms.Domain.Entities;

namespace DTR.Core;

/// <summary>
/// Resolves the <see cref="WorkType"/> for a shift by evaluating attendance, leave,
/// rest-day, and holiday status against a fixed priority order:
///
///   1. Incomplete attendance         — always wins; nothing else matters if the punches don't pair up.
///   2. Worked while on leave         — half-day leave: attendance plus an active leave
///                                       application for the same day. Wins outright over
///                                       rest-day and holiday duty classification below.
///   3. Worked the shift (pure duty)  — rest day / legal / special holiday duty variants,
///                                       else regular work.
///   4. On leave (no duty)            — leave on a rest day collapses to a plain rest day;
///                                       leave on a holiday is recorded as holiday-leave;
///                                       otherwise paid/unpaid leave per the leave's pay type.
///   5. Rest day (no duty, no leave).
///   6. Holiday (no duty, no leave, not a rest day) — legal, then special.
///   7. Absent — fallback when none of the above apply.
///
/// This order is significant and intentional; do not reorder branches without
/// confirming the payroll rules that depend on it.
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
        var isSpecialHoliday = context.IsSpecialHoliday();
        var isNonWorkingSpecialHoliday = isSpecialHoliday && IsNonWorkingSpecialHoliday(context);

        if (hasAttendance)
            return ResolveDutyWorkType(leave, isRestDay, isLegalHoliday, isSpecialHoliday, isNonWorkingSpecialHoliday);

        if (leave != null)
            return ResolveLeaveWorkType(leave, isRestDay, isLegalHoliday, isSpecialHoliday);

        if (isRestDay)
            return WorkType.RestDay;

        if (isLegalHoliday)
            return WorkType.RegularHoliday;

        if (isSpecialHoliday)
            return isNonWorkingSpecialHoliday ? WorkType.SpecialNonWorking : WorkType.SpecialHoliday;

        return WorkType.Absent;
    }

    /// <summary>
    /// Employee has attendance for the shift. If a leave application is also active for
    /// the same day (half-day leave), that combined classification wins outright over
    /// rest-day and holiday duty classification. Otherwise resolves pure duty.
    /// </summary>
    private static WorkType ResolveDutyWorkType(LeaveApplication? leave, bool isRestDay, bool isLegalHoliday, bool isSpecialHoliday, bool isNonWorkingSpecialHoliday)
    {
        if (leave != null)
            return ResolveDutyOnLeaveWorkType(leave);

        if (isRestDay)
        {
            if (isLegalHoliday) return WorkType.RestDayLegalHolidayDuty;
            if (isSpecialHoliday) return WorkType.RestDaySpecialHolidayDuty;
            return WorkType.RestDayDuty;
        }

        if (isLegalHoliday)
            return WorkType.RegularHolidayDuty;

        if (isSpecialHoliday)
            return isNonWorkingSpecialHoliday ? WorkType.SpecialHolidayDutyNW : WorkType.SpecialHolidayDuty;

        return WorkType.RegularWorkDay;
    }

    /// <summary>
    /// Employee worked part of the shift while also having an active leave application
    /// covering the same day (half-day leave). Classified purely by the leave's pay type —
    /// this does not fall through to rest-day or holiday duty resolution.
    /// </summary>
    private static WorkType ResolveDutyOnLeaveWorkType(LeaveApplication leave) =>
        leave.PayType == PayType.WithPay
            ? WorkType.PaidLeaveDuty
            : WorkType.UnpaidLeaveDuty;

    /// <summary>
    /// Employee has no attendance but is on leave. Leave takes precedence over plain
    /// absence/holiday classification, except that leave on a rest day is recorded
    /// as simply a rest day (the leave itself is moot — the employee wasn't
    /// scheduled to work regardless).
    /// </summary>
    private static WorkType ResolveLeaveWorkType(LeaveApplication leave, bool isRestDay, bool isLegalHoliday, bool isSpecialHoliday)
    {
        if (isRestDay)
            return WorkType.RestDay;

        if (isLegalHoliday)
            return WorkType.PaidLeaveOnLegalHoliday;

        if (isSpecialHoliday)
            return WorkType.PaidLeaveOnSpecialHoliday;

        return leave.PayType == PayType.WithPay
            ? WorkType.PaidLeave
            : WorkType.UnpaidLeave;
    }

    private static bool IsNonWorkingSpecialHoliday(TimeContext context)
    {
        var specialHoliday = context.Payload.Provider.HolidayProvider.GetHolidayInfoDuringShift(
            HolidayType.SPECIAL,
            context.Payload.Data.Employee,
            context.Payload.Data.CurrentShift);

        return specialHoliday?.WorkType == HolidayWorkType.NonWorking;
    }
}