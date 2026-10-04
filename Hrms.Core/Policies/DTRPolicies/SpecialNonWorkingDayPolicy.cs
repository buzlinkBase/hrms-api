using Hrms.Core.Calculators.DtrPay;

namespace Hrms.Core.Policies.DTRPolicies;

internal class SpecialNonWorkingDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        var hours = DayTypeHours.For(context, dailyRecord.SpecialHolHours, dailyRecord.SpecialHolNightDiffHours, dailyRecord.SpecialHolNightDiffOTHours);
        // Plain hours keep this day type's own rules below; under Additive the night hours inside
        // SpecialHolHours are then added at the full day rate (DayTypeHours). Night is added after the
        // plain pay because the plain pay sets line.Worked rather than adding to it.
        return hours.AddNightPay(PayPlainHours(line, context, hours.Plain), context, PayCategories.Special);
    }

    // This day type's pay for its plain (daytime) hours -- unchanged from before the DTR stored
    // natural hours: holiday eligibility, Fixed-salary pre-funding, unworked = shift - plain.
    private static BasicPipelineData PayPlainHours(BasicPipelineData line, PayrollContext context, decimal plainHours)
    {
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.WorkTypeEnum is not (WorkType.SpecialNonWorkingHoliday 
                or WorkType.SpecialHolidayDuty) ||
            dailyRecord.ShiftWorkingHour <= 0)
        {
            return line;
        }

        var hourlyRate = RateHelper.GetHourlyRate(context);
        var workedHours = plainHours;
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