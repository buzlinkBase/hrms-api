using Hrms.Core.Calculators.DtrPay;

namespace Hrms.Core.Policies.DTRPolicies;

/// <summary>
/// Pays the "Double Legal Holiday" day (two regular holidays coinciding on the same
/// calendar date) — reads DailyRecord.DoubleLegalHours.
/// DOLE Standard:
/// - Unworked: 200% (2.0x daily rate)
/// - Worked: 300% (3.0x daily rate for regular 8 hours)
/// </summary>
internal class DoubleLegalPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        var hours = DayTypeHours.For(context, dailyRecord.DoubleLegalHours, dailyRecord.DoubleLegalNDHours, dailyRecord.DoubleLegalNDOTHours);
        // Additive pays the natural hours (night hours included) at this day type's rate, so night
        // differential only adds its premium; Compounded pays the daytime hours only (DayTypeHours).
        return PayHours(line, context, hours.PaidHours(context, dailyRecord.DoubleLegalHours));
    }

    // This day type's pay for the hours it pays at its own rate: holiday eligibility,
    // Fixed-salary pre-funding, unworked = shift - paid.
    private static BasicPipelineData PayHours(BasicPipelineData line, PayrollContext context, decimal paidHours)
    {
        // Holiday Hours Basis (BasedOnTimeInDayType vs BasedOnActualWorkHours) is already
        // applied upstream by dtr-api's HolidayPolicyProviderFactory when DoubleLegalHours is
        // computed — see CleanDTRDetailProcessor.cs. Nothing to branch on here.
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.WorkTypeEnum is not (WorkType.DoubleLegal or WorkType.DoubleLegalDuty) ||
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

        // DOLE: worked double holiday = 300% (3.0x), unworked = 200% (2.0x).
        var earnings = DayTypePayCalculator
            .For(isEligible, isBasePayPreFunded, workedMultiplier: 3.0m, unworkedMultiplier: 2.0m, line)
            .CalculateWorkedPay(hourlyRate, workedHours)
            .CalculateUnworkedPay(hourlyRate, unworkedHours)
            .Total;

        line.Value += earnings;
        return line;
    }
}