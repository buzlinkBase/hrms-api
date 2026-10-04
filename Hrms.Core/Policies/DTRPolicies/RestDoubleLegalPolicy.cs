using Hrms.Core.Calculators.DtrPay;

namespace Hrms.Core.Policies.DTRPolicies;

internal class RestDoubleLegalPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        var hours = DayTypeHours.For(context, dailyRecord.RestDoubleLegalHours, dailyRecord.RestDoubleLegalNDHours, dailyRecord.RestDoubleLegalNDOTHours);
        // Plain hours keep this day type's own rules below; under Additive the night hours inside
        // RestDoubleLegalHours are then added at the full day rate (DayTypeHours). Night is added after the
        // plain pay because the plain pay sets line.Worked rather than adding to it.
        return hours.AddNightPay(PayPlainHours(line, context, hours.Plain), context, PayCategories.RestDoubleLegal);
    }

    // This day type's pay for its plain (daytime) hours -- unchanged from before the DTR stored
    // natural hours: holiday eligibility, Fixed-salary pre-funding, unworked = shift - plain.
    private static BasicPipelineData PayPlainHours(BasicPipelineData line, PayrollContext context, decimal plainHours)
    {
        // Holiday Hours Basis (BasedOnTimeInDayType vs BasedOnActualWorkHours) is already
        // applied upstream by dtr-api's HolidayPolicyProviderFactory when RestDoubleLegalHours
        // is computed — see CleanDTRDetailProcessor.cs. Nothing to branch on here.
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.WorkTypeEnum is not (WorkType.RestDayDoubleLegal or WorkType.RestDayDoubleLegalDuty) ||
            dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.HolCount == 0)
        {
            return line;
        }

        var hourlyRate = RateHelper.GetHourlyRate(context);
        var workedHours = plainHours;
        var unworkedHours = Math.Max(0m, (decimal)dailyRecord.ShiftWorkingHour - workedHours);

        var isEligible = new IsEligibleForRegularHolidayPay().IsSatisfiedBy(context);
        var isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                                  context.Employee.IsRestDayPaid &&
                                  context.Employee.IsRegularHolidayIncluded;

        // DOLE: worked rest day + double holiday = 390% (300% double holiday + 30% rest day
        // premium), unworked double holiday = 200%. Pre-funded delta on unworked is the same
        // 2.0 - 1.0 = 1.0 as DoubleLegalPolicy — base pay covers the 1.0x, premium remains due.
        var earnings = DayTypePayCalculator
            .For(isEligible, isBasePayPreFunded, workedMultiplier: 3.9m, unworkedMultiplier: 2.0m, line)
            .CalculateWorkedPay(hourlyRate, workedHours)
            .CalculateUnworkedPay(hourlyRate, unworkedHours)
            .Total;

        line.Value += earnings;
        return line;
    }
}