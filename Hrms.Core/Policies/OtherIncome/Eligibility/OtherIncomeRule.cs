using Hrms.Domain.Entities;

namespace Hrms.Core.Policies.OtherIncome.Eligibility;

// The attendance rules of one Other Income application, detached from the entity so the
// eligibility chain stays a pure function of (rule, day, DTR record).
public sealed record OtherIncomeRule(
    OtherIncomePayMode PayMode,
    OtherIncomeAttendanceBasis Basis,
    decimal? MinHours,
    int? MaxLateMinutes,
    int? MaxUndertimeMinutes,
    int ApplicableDays,
    int DayTypes,
    int? MinQualifyingDays)
{
    public static OtherIncomeRule From(OtherIncomeApplication application) => new(
        application.PayMode,
        application.AttendanceBasis,
        application.MinHours,
        application.MaxLateMinutes,
        application.MaxUndertimeMinutes,
        application.ApplicableDays,
        application.DayTypes,
        application.MinQualifyingDays);
}

// One calendar day under evaluation. Record is null when no posted DTR exists for that day.
public sealed record DayContext(DateOnly Date, DailyRecord? Record, OtherIncomeRule Rule);

// A link in the chain: returns null when the day passes this rule, else an
// OtherIncomeSkipReasons code and the chain stops there.
public interface IDayEligibilityRule
{
    string? Check(DayContext day);
}
