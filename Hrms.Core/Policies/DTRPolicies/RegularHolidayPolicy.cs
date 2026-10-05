using Hrms.Core.Calculators.DtrPay;

namespace Hrms.Core.Policies.DTRPolicies;
public class RegularHolidayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        var hours = DayTypeHours.For(context, dailyRecord.LegalHolHours, dailyRecord.LegalHolNightDiffHours, dailyRecord.LegalHolNightDiffOTHours);
        // Additive pays the natural hours (night hours included) at this day type's rate, so night
        // differential only adds its premium; Compounded pays the daytime hours only (DayTypeHours).
        return PayHours(line, context, hours.PaidHours(context, dailyRecord.LegalHolHours));
    }

    // This day type's pay for the hours it pays at its own rate: holiday eligibility,
    // Fixed-salary pre-funding, unworked = shift - paid.
    private static BasicPipelineData PayHours(BasicPipelineData line, PayrollContext context, decimal paidHours)
    {
        // Holiday Hours Basis (BasedOnTimeInDayType vs BasedOnActualWorkHours) is already
        // applied upstream by dtr-api's HolidayPolicyProviderFactory when LegalHolHours is
        // computed — see CleanDTRDetailProcessor.cs. Nothing to branch on here.
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.WorkTypeEnum is not (WorkType.LegalHoliday or WorkType.LegalHolidayDuty) ||
            dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.HolCount == 0)
        {
            return line;
        }

        var hourlyRate = RateHelper.GetHourlyRate(context);
        var workedHours = paidHours;
        var unworkedHours = Math.Max(0m, (decimal)dailyRecord.ShiftWorkingHour - workedHours);

        var isEligible = new IsEligibleForRegularHolidayPay().IsSatisfiedBy(context);
        var isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                                  context.Employee.IsRegularHolidayIncluded;
        // Eligible & Pre-Funded (Fixed): Earns delta premium above 1.0x base pay (e.g., 2.0x - 1.0x = 1.0x)
        // Eligible & Not Pre-Funded (Daily): Earns full configured worked rate multiplier (2.0x)
        // Unworked-but-eligible hours are paid in full when not pre-funded (regular holidays are
        // paid even unworked), or not at all when pre-funded (already covered by base pay).
        var rateMultiplier = PremiumRateHelper.GetRate(context, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY);

        var earnings = DayTypePayCalculator
            .For(isEligible, isBasePayPreFunded, rateMultiplier, unworkedMultiplier: 1.0m, line)
            .CalculateWorkedPay(hourlyRate, workedHours)
            .CalculateUnworkedPay(hourlyRate, unworkedHours)
            .Total;

        line.Value += earnings;
        return line;
    }
}