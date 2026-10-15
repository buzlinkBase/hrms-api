using Hrms.Domain.Entities;

namespace Hrms.Core.Policies.OtherIncome.Eligibility;

// "Does this day count at all for the allowance?" -- the weekday mask.
public sealed class ApplicableWeekdayRule : IDayEligibilityRule
{
    public string? Check(DayContext day)
    {
        var bit = WeekdayBit(day.Date.DayOfWeek);
        return (day.Rule.ApplicableDays & bit) != 0 ? null : OtherIncomeSkipReasons.NotApplicableDay;
    }

    // Monday = 1 ... Sunday = 64, matching OtherIncomeWeekdays.
    public static int WeekdayBit(DayOfWeek dayOfWeek) => 1 << (((int)dayOfWeek + 6) % 7);
}

// "Does this kind of day count?" -- regular / rest day / legal / special holiday, read off the
// DTR's WorkType. With no DTR the kind is unknown and the day is treated as regular.
public sealed class DayTypeRule : IDayEligibilityRule
{
    public string? Check(DayContext day)
    {
        var type = day.Record == null ? OtherIncomeDayTypes.Regular : Classify(day.Record.WorkTypeEnum);
        return (day.Rule.DayTypes & (int)type) != 0 ? null : OtherIncomeSkipReasons.DayTypeExcluded;
    }

    // A holiday that falls on a rest day counts as the holiday, not the rest day.
    public static OtherIncomeDayTypes Classify(WorkType workType) => workType switch
    {
        WorkType.LegalHoliday or WorkType.LegalHolidayDuty
            or WorkType.RestDayLegalHoliday or WorkType.RestDayLegalHolidayDuty
            or WorkType.DoubleLegal or WorkType.DoubleLegalDuty
            or WorkType.RestDayDoubleLegal or WorkType.RestDayDoubleLegalDuty => OtherIncomeDayTypes.LegalHoliday,

        WorkType.SpecialNonWorkingHoliday or WorkType.SpecialHolidayDuty
            or WorkType.RestDaySpecialHoliday or WorkType.RestDaySpecialHolidayDuty => OtherIncomeDayTypes.SpecialHoliday,

        WorkType.RestDay or WorkType.RestDayDuty or WorkType.RestDayTravel => OtherIncomeDayTypes.RestDay,

        _ => OtherIncomeDayTypes.Regular,
    };
}

// "Was the employee there?" -- no posted DTR, or the basis (worked / worked-or-paid-leave) failed.
public sealed class AttendanceBasisRule : IDayEligibilityRule
{
    public string? Check(DayContext day)
    {
        if (day.Rule.Basis == OtherIncomeAttendanceBasis.None) return null;
        if (day.Record == null) return OtherIncomeSkipReasons.NoDtr;
        if (Worked(day.Record)) return null;
        if (day.Rule.Basis == OtherIncomeAttendanceBasis.WorkedOrPaidLeave && IsPaidLeave(day.Record)) return null;
        return OtherIncomeSkipReasons.NotWorked;
    }

    public static bool Worked(DailyRecord record) => record.TotalHours > 0 || record.OBHours > 0;

    public static bool IsPaidLeave(DailyRecord record) =>
        record.PaidLeaveHours > 0 || record.WorkTypeEnum is WorkType.PaidLeave or WorkType.GovFundedLeave;
}

// Minimum worked hours. A paid-leave day that the basis accepts is exempt: leave isn't worked hours.
public sealed class MinHoursRule : IDayEligibilityRule
{
    public string? Check(DayContext day)
    {
        if (day.Rule.MinHours is not { } min || day.Record == null) return null;
        if (day.Rule.Basis == OtherIncomeAttendanceBasis.WorkedOrPaidLeave
            && !AttendanceBasisRule.Worked(day.Record) && AttendanceBasisRule.IsPaidLeave(day.Record))
            return null;
        return (decimal)day.Record.TotalHours >= min ? null : OtherIncomeSkipReasons.BelowMinHours;
    }
}

// Late / undertime limits in minutes (0 = no tolerance, null = not checked).
public sealed class PunctualityRule : IDayEligibilityRule
{
    public string? Check(DayContext day)
    {
        var record = day.Record;
        if (record == null) return null;
        if (day.Rule.MaxLateMinutes is { } maxLate && record.LateMinutes > maxLate)
            return OtherIncomeSkipReasons.TooLate;
        if (day.Rule.MaxUndertimeMinutes is { } maxUt && record.UTMinutes > maxUt)
            return OtherIncomeSkipReasons.TooMuchUndertime;
        return null;
    }
}
