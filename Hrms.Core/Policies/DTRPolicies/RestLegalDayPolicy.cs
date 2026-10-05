using Hrms.Core.Calculators.DtrPay;

namespace Hrms.Core.Policies.DTRPolicies;

/// <summary>
/// Pays the "Rest Day + Legal Holiday" combo day — reads DailyRecord.RestLegalDayHours.
/// Mirrors RegularHolidayPolicy's WorkType-gated worked/unworked split and eligibility
/// handling (worked pay uses the configured multiplier, unworked-but-eligible hours are
/// credited at 1.0x), but requires BOTH IsRestDayPaid and IsRegularHolidayIncluded to treat
/// the combo premium as pre-funded — since the day is simultaneously a rest day and a
/// holiday — and uses the combo rate (RESTDAY_DUTY * LEGAL_HOLIDAY_DUTY) instead of the
/// plain holiday rate. Distinct from RegularHolidayPolicy, which only covers pure
/// LegalHoliday/LegalHolidayDuty days and reads LegalHolHours.
/// </summary>
internal class RestLegalDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        var hours = DayTypeHours.For(context, dailyRecord.RestLegalDayHours, dailyRecord.RestLegalDayNDHours, dailyRecord.RestLegalDayNDOTHours);
        // Additive pays the natural hours (night hours included) at this day type's rate, so night
        // differential only adds its premium; Compounded pays the daytime hours only (DayTypeHours).
        return PayHours(line, context, hours.PaidHours(context, dailyRecord.RestLegalDayHours));
    }

    // This day type's pay for the hours it pays at its own rate: holiday eligibility,
    // Fixed-salary pre-funding, unworked = shift - paid.
    private static BasicPipelineData PayHours(BasicPipelineData line, PayrollContext context, decimal paidHours)
    {
        var dailyRecord = context.DailyRecord;

        if (dailyRecord.WorkTypeEnum is not (WorkType.RestDayLegalHoliday or WorkType.RestDayLegalHolidayDuty) ||
            dailyRecord.ShiftWorkingHour <= 0)
        {
            return line;
        }

        var hourlyRate = RateHelper.GetHourlyRate(context);
        var workedHours = paidHours;
        var unworkedHours = Math.Max(0m, (decimal)dailyRecord.ShiftWorkingHour - workedHours);

        var isEligible = new IsEligibleForRegularHolidayPay().IsSatisfiedBy(context);
        // Requires BOTH IsRestDayPaid and IsRegularHolidayIncluded to treat the combo premium
        // as pre-funded, since the day is simultaneously a rest day and a holiday.
        var isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                                  context.Employee.IsRestDayPaid &&
                                  context.Employee.IsRegularHolidayIncluded;
        var rateMultiplier =
            PremiumRateHelper.GetRate(context, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY) *
            PremiumRateHelper.GetRate(context, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY);

        var earnings = DayTypePayCalculator
            .For(isEligible, isBasePayPreFunded, rateMultiplier, unworkedMultiplier: 1.0m, line)
            .CalculateWorkedPay(hourlyRate, workedHours)
            .CalculateUnworkedPay(hourlyRate, unworkedHours)
            .Total;

        line.Value += earnings;
        return line;
    }
}