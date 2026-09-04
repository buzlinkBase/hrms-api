
namespace Hrms.Core.Policies.DTRPolicies;
public class RegularHolidayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
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
        var workedHours = (decimal)Math.Max(0, dailyRecord.LegalHolHours);
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