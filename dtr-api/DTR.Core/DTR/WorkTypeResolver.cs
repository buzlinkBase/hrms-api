using Hrms.Domain.Entities;

namespace DTR.Core;

public interface IWorkTypeResolver
{
    WorkType Resolve(TimeContext context);
}

/// <summary>
/// Resolves the <see cref="WorkType"/> for a shift against a fixed priority order:
///
///   1. Incomplete attendance       — always wins; nothing else matters if punches don't pair up.
///   2. Rest day / holiday duty     — employee worked; rest-day and holiday classifications beat leave.
///   3. Leave duty                  — employee worked on a regular day while on (partial) leave.
///   4. Regular duty                — employee worked a normal shift.
///   5. Rest day (no duty)          — including holiday combinations on a rest day.
///   6. Legal holiday (no duty)     — beats leave when employee did not work.
///   7. Special non-working (no duty) — beats leave when employee did not work.
///   8. On leave (no duty, regular day).
///   9. Special working / travel / absent — fallbacks.
/// </summary>
public class WorkTypeResolver : IWorkTypeResolver
{
    public WorkType Resolve(TimeContext context)
    {
        var attendance = context.Payload.Provider.AttendanceProvider.CurrentShiftAttendance();
        var hasAttendance = attendance.Any();
        var hasIncompleteAttendance = hasAttendance && attendance.Count % 2 != 0;
        if (hasIncompleteAttendance)
            return WorkType.Incomplete;

        var leave = context.Payload.Data.CurrentLeaves;
        var travel = context.Payload.Data.CurrentTravel;
        var isRestDay = new IsRestDaySpec().IsSatisfiedBy(context.CanonicalTimeRange, context);
        var isLegalHoliday = context.IsLegalHoliday();
        var isDoubleLegal = isLegalHoliday && context.IsDoubleLegalHoliday();
        var isSpecialWorking = context.IsSpecialWorking();
        var isSpecialNonWorking = context.IsSpecialNonWorking();

        var isGovFundedLeaved = new IsGovFundedLeaved()
            .IsSatisfiedBy(TimeRange.Empty, context);

        if (isGovFundedLeaved)
        {
            return WorkType.GovFundedLeave;
        }

        if (hasAttendance)
            return ResolveDutyWorkType(leave, isRestDay, isLegalHoliday, isDoubleLegal, isSpecialWorking, isSpecialNonWorking);

        // No attendance — rest day and holidays outrank leave
        if (isRestDay)
        {
            if (isDoubleLegal) return WorkType.RestDayDoubleLegal;
            if (isLegalHoliday) return WorkType.RestDayLegalHoliday;
            if (isSpecialNonWorking) return WorkType.RestDaySpecialHoliday;
            return WorkType.RestDay;
        }

        if (isDoubleLegal) return WorkType.DoubleLegal;
        if (isLegalHoliday)
            return WorkType.LegalHoliday;

        if (isSpecialNonWorking)
            return WorkType.SpecialNonWorkingHoliday;

        if (leave != null && leave.Count > 0)
            return ResolveLeaveWorkType(leave);

        if (isSpecialWorking)
            return WorkType.Absent;

        if (travel != null)
            return ResolveTravelWorkType(travel, isRestDay);

        return WorkType.Absent;
    }

    /// <summary>
    /// Employee has attendance. Rest day and holiday classifications outrank leave.
    /// Leave only wins on a plain regular working day.
    /// </summary>
    private static WorkType ResolveDutyWorkType(
        List<LeaveApplication> leave,
        bool isRestDay,
        bool isLegalHoliday,
        bool isDoubleLegal,
        bool isSpecialWorking,
        bool isSpecialNonWorking)
    {

        if (isRestDay)
        {
            if (isDoubleLegal) return WorkType.RestDayDoubleLegalDuty;
            if (isLegalHoliday) return WorkType.RestDayLegalHolidayDuty;
            if (isSpecialNonWorking) return WorkType.RestDaySpecialHolidayDuty;
            return WorkType.RestDayDuty;
        }

        if (isDoubleLegal) return WorkType.DoubleLegalDuty;
        if (isLegalHoliday)
            return WorkType.LegalHolidayDuty;

        if (isSpecialNonWorking)
            return WorkType.SpecialHolidayDuty;

        if (isSpecialWorking)
            return WorkType.SpecialWorkingHoliday;

        if (leave != null && leave.Count > 0)
            return ResolveDutyOnLeaveWorkType(leave);

        return WorkType.RegularWorkDay;
    }

    /// <summary>
    /// Employee worked on a regular day while on (partial) leave. A day can have more than
    /// one leave application in flight — any paid leave present outranks unpaid.
    /// </summary>
    private static WorkType ResolveDutyOnLeaveWorkType(List<LeaveApplication> leave) =>
        leave.Any(x => x.PayType == PayType.WithPay)
            ? WorkType.PaidLeave
            : WorkType.UnpaidLeave;

    /// <summary>
    /// Employee has no attendance and is on leave on a regular working day. A day can have
    /// more than one leave application in flight — any paid leave present outranks unpaid.
    /// </summary>
    private static WorkType ResolveLeaveWorkType(List<LeaveApplication> leave) =>
        leave.Any(x => x.PayType == PayType.WithPay)
            ? WorkType.PaidLeave
            : WorkType.UnpaidLeave;

    private static WorkType ResolveTravelWorkType(TravelOrderApplication travel, bool isRestDay)
    {
        if (isRestDay && travel != null)
            return WorkType.RestDayTravel;

        if (isRestDay)
            return WorkType.RestDay;

        return WorkType.Travel;
    }
}
