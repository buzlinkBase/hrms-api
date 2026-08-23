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

        if (dailyRecord.WorkTypeEnum is not (WorkType.RestDayLegalHoliday or WorkType.RestDayLegalHolidayDuty) ||
            dailyRecord.ShiftWorkingHour <= 0)
        {
            return line;
        }

        var hourlyRate = context.Employee.DailyRate / (decimal)dailyRecord.ShiftWorkingHour;
        var workedHours = (decimal)Math.Max(0, dailyRecord.RestLegalDayHours);
        var unworkedHours = Math.Max(0m, (decimal)dailyRecord.ShiftWorkingHour - workedHours);

        var earnings = RestLegalDayPayCalculator
            .ForContext(context)
            .CalculateWorkedPay(hourlyRate, workedHours)
            .CalculateUnworkedPay(hourlyRate, unworkedHours)
            .Total;

        line.Value += earnings;
        return line;
    }
}

public class RestLegalDayPayCalculator
{
    private decimal _total;
    private readonly bool _isEligible;
    private readonly bool _isBasePayPreFunded;
    private readonly decimal _totalRateMultiplier;

    private RestLegalDayPayCalculator(PayrollContext context)
    {
        _isEligible = new IsEligibleForHolidayPay().IsSatisfiedBy(context);
        _isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                              context.Employee.IsRestDayPaid &&
                              context.Employee.IsRegularHolidayIncluded;
        _totalRateMultiplier =
            PremiumRateHelper.GetRate(context, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY) *
            PremiumRateHelper.GetRate(context, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY);
    }

    public static RestLegalDayPayCalculator ForContext(PayrollContext context) => new(context);

    public RestLegalDayPayCalculator CalculateWorkedPay(decimal hourlyRate, decimal workedHours)
    {
        if (workedHours <= 0) return this;
        // Ineligible: Earns 1.0x standard rate
        // Eligible & Pre-Funded (rest day AND holiday both already covered by base pay):
        //   Earns delta premium above 1.0 (e.g., 2.60 - 1.00 = 1.60)
        // Eligible & Not Pre-Funded: Earns full configured combo multiplier (e.g., 2.60)
        var multiplier = !_isEligible
            ? 1.0m
            : (_isBasePayPreFunded ? Math.Max(0m, _totalRateMultiplier - 1.0m) : _totalRateMultiplier);

        _total += hourlyRate * workedHours * multiplier;
        return this;
    }

    public RestLegalDayPayCalculator CalculateUnworkedPay(decimal hourlyRate, decimal unworkedHours)
    {
        if (unworkedHours <= 0 || !_isEligible || _isBasePayPreFunded) return this;

        // Unworked rest day + legal holiday credit is paid at 100% (1.0x) base rate
        _total += hourlyRate * unworkedHours * 1.0m;
        return this;
    }
    public decimal Total => _total;
}
