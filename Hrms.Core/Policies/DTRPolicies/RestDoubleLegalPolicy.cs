namespace Hrms.Core.Policies.DTRPolicies;

/// <summary>
/// Pays the "Rest Day + Double Legal Holiday" combo day — reads
/// DailyRecord.RestDoubleLegalHours. Mirrors RegularHolidayPolicy/RestLegalDayPolicy's
/// WorkType-gated worked/unworked split and eligibility handling, but requires BOTH
/// IsRestDayPaid and IsRegularHolidayIncluded to treat the combo premium as pre-funded, and
/// uses the combo rate (RESTDAY_DUTY * LEGAL_HOLIDAY_DUTY * 2).
/// </summary>
internal class RestDoubleLegalPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;

        if (dailyRecord.WorkTypeEnum is not (WorkType.RestDayDoubleLegal or WorkType.RestDayDoubleLegalDuty) ||
            dailyRecord.ShiftWorkingHour <= 0)
        {
            return line;
        }

        var hourlyRate = context.Employee.DailyRate / (decimal)dailyRecord.ShiftWorkingHour;
        var workedHours = (decimal)Math.Max(0, dailyRecord.RestDoubleLegalHours);
        var unworkedHours = Math.Max(0m, (decimal)dailyRecord.ShiftWorkingHour - workedHours);

        var earnings = RestDoubleLegalPayCalculator
            .ForContext(context)
            .CalculateWorkedPay(hourlyRate, workedHours)
            .CalculateUnworkedPay(hourlyRate, unworkedHours)
            .Total;

        line.Value += earnings;
        return line;
    }
}

public class RestDoubleLegalPayCalculator
{
    private decimal _total;
    private readonly bool _isEligible;
    private readonly bool _isBasePayPreFunded;
    private readonly decimal _totalRateMultiplier;

    private RestDoubleLegalPayCalculator(PayrollContext context)
    {
        _isEligible = new IsEligibleForHolidayPay().IsSatisfiedBy(context);
        _isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                              context.Employee.IsRestDayPaid &&
                              context.Employee.IsRegularHolidayIncluded;
        _totalRateMultiplier =
            PremiumRateHelper.GetRate(context, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY) *
            ((Math.Max(2m, context.DailyRecord.HolCount)) * PremiumRateHelper.GetRate(context, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY));
    }

    public static RestDoubleLegalPayCalculator ForContext(PayrollContext context) => new(context);

    public RestDoubleLegalPayCalculator CalculateWorkedPay(decimal hourlyRate, decimal workedHours)
    {
        if (workedHours <= 0) return this;
        // Ineligible: Earns 1.0x standard rate
        // Eligible & Pre-Funded (rest day AND double holiday both already covered by base pay):
        //   Earns delta premium above 1.0
        // Eligible & Not Pre-Funded: Earns full configured combo multiplier
        var multiplier = !_isEligible
            ? 1.0m
            : (_isBasePayPreFunded ? Math.Max(0m, _totalRateMultiplier - 1.0m) : _totalRateMultiplier);

        _total += hourlyRate * workedHours * multiplier;
        return this;
    }

    public RestDoubleLegalPayCalculator CalculateUnworkedPay(decimal hourlyRate, decimal unworkedHours)
    {
        if (unworkedHours <= 0 || !_isEligible || _isBasePayPreFunded) return this;

        // Unworked rest day + double legal holiday credit is paid at 100% (1.0x) base rate
        _total += hourlyRate * unworkedHours * 1.0m;
        return this;
    }

    public decimal Total => _total;
}
