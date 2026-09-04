namespace Hrms.Core.Policies.DTRPolicies;

/// <summary>
/// Pays the "Rest Day + Special Non-Working Holiday" combo day — reads
/// DailyRecord.RestSpecialDayHours. Mirrors RegularHolidayPolicy/RestLegalDayPolicy's
/// WorkType-gated worked/unworked split and eligibility handling, but requires BOTH
/// IsRestDayPaid and IsSpecialNonWorkingIncluded to treat the combo premium as pre-funded,
/// and uses the combo rate (RESTDAY_SPECIAL) instead of the plain special-holiday rate.
/// Distinct from SpecialWorkDayPolicy, which reads SpecialHolHours for pure special-holiday
/// days (SpecialNonWorkingHoliday / SpecialHolidayDuty WorkTypes without a rest-day overlap).
/// </summary>
internal class RestSpecialDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;

        if (dailyRecord.WorkTypeEnum is not (WorkType.RestDaySpecialHoliday or WorkType.RestDaySpecialHolidayDuty) ||
            dailyRecord.ShiftWorkingHour <= 0)
        {
            return line;
        }

        var hourlyRate = RateHelper.GetHourlyRate(context);
        var workedHours = (decimal)Math.Max(0, dailyRecord.RestSpecialDayHours);
        var unworkedHours = Math.Max(0m, (decimal)dailyRecord.ShiftWorkingHour - workedHours);

        var isEligible = new IsEligibleForSpecialHolidayPay().IsSatisfiedBy(context);
        // Requires BOTH IsRestDayPaid and IsSpecialNonWorkingIncluded to treat the combo
        // premium as pre-funded, since the day is simultaneously a rest day and a special
        // non-working holiday.
        var isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                                  context.Employee.IsRestDayPaid &&
                                  context.Employee.IsSpecialNonWorkingIncluded;
        var rateMultiplier = PremiumRateHelper.GetRate(context, RateType.RESTDAY_SPECIAL, RATE_DEFAULT.RESTDAY_SPECIAL);

        // DOLE Standard: "No Work, No Pay" applies to Special Non-Working Holidays, even
        // combined with a rest day — unworkedMultiplier: 0m means CalculateUnworkedPay
        // always contributes nothing.
        var earnings = DayTypePayCalculator
            .For(isEligible, isBasePayPreFunded, rateMultiplier, unworkedMultiplier: 0m)
            .CalculateWorkedPay(hourlyRate, workedHours)
            .CalculateUnworkedPay(hourlyRate, unworkedHours)
            .Total;

        line.Value += earnings;
        return line;
    }
}