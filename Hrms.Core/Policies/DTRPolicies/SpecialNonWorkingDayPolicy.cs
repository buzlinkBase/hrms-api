using Hrms.Core.Calculators.DtrPay;

namespace Hrms.Core.Policies.DTRPolicies;

internal class SpecialNonWorkingDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        var hours = DayTypeHours.For(context, dailyRecord.SpecialHolHours, dailyRecord.SpecialHolNightDiffHours, dailyRecord.SpecialHolNightDiffOTHours);
        // Additive pays the natural hours (night hours included) at this day type's rate, so night
        // differential only adds its premium; Compounded pays the daytime hours only (DayTypeHours).
        return PayHours(line, context, hours.PaidHours(context, dailyRecord.SpecialHolHours));
    }

    // This day type's pay for the hours it pays at its own rate: holiday eligibility,
    // Fixed-salary pre-funding, unworked = shift - paid.
    private static BasicPipelineData PayHours(BasicPipelineData line, PayrollContext context, decimal paidHours)
    {
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.WorkTypeEnum is not (WorkType.SpecialNonWorkingHoliday 
                or WorkType.SpecialHolidayDuty) ||
            dailyRecord.ShiftWorkingHour <= 0)
        {
            return line;
        }

        var hourlyRate = RateHelper.GetHourlyRate(context);
        var workedHours = paidHours;
        var unworkedHours = Math.Max(0m, (decimal)dailyRecord.ShiftWorkingHour - workedHours);

        var isEligible = new IsEligibleForSpecialHolidayPay().IsSatisfiedBy(context);
        var isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                                  context.Employee.IsSpecialNonWorkingIncluded;
        var rateMultiplier = PremiumRateHelper.GetRate(context, RateType.SPECIAL_NON_WORKING, RATE_DEFAULT.SPECIAL_NON_WORKING);

        // DOLE Standard: "No Work, No Pay" applies to Special Non-Working Holidays —
        // unworkedMultiplier: 0m means CalculateUnworkedPay always contributes nothing.
        var earnings = DayTypePayCalculator
            .For(isEligible, isBasePayPreFunded, rateMultiplier, unworkedMultiplier: 0m)
            .CalculateWorkedPay(hourlyRate, workedHours)
            .CalculateUnworkedPay(hourlyRate, unworkedHours)
            .Total;

        line.Value += earnings;
        return line;
    }
}