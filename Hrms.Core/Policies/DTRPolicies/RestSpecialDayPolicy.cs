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

        var earnings = RestSpecialDayPayCalculator
            .ForContext(context)
            .CalculateWorkedPay(hourlyRate, workedHours)
            .CalculateUnworkedPay(hourlyRate, unworkedHours)
            .Total;

        line.Value += earnings;
        return line;
    }
}

public class RestSpecialDayPayCalculator
{
    private decimal _total;
    private readonly bool _isEligible;
    private readonly bool _isBasePayPreFunded;
    private readonly decimal _totalRateMultiplier;

    private RestSpecialDayPayCalculator(PayrollContext context)
    {
        _isEligible = new IsEligibleForHolidayPay().IsSatisfiedBy(context);
        _isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                              context.Employee.IsRestDayPaid &&
                              context.Employee.IsSpecialNonWorkingIncluded;
        _totalRateMultiplier = PremiumRateHelper.GetRate(context, RateType.RESTDAY_SPECIAL, RATE_DEFAULT.RESTDAY_SPECIAL);
    }

    public static RestSpecialDayPayCalculator ForContext(PayrollContext context) => new(context);

    public RestSpecialDayPayCalculator CalculateWorkedPay(decimal hourlyRate, decimal workedHours)
    {
        if (workedHours <= 0) return this;

        // Ineligible: Earns 1.0x standard hourly rate
        // Eligible & Pre-Funded (rest day AND special holiday both already covered by base pay):
        //   Earns delta premium above 1.0x base pay (e.g., 1.50 - 1.00 = 0.50)
        // Eligible & Not Pre-Funded: Earns full configured combo multiplier (e.g., 1.50)
        var multiplier = !_isEligible
            ? 1.0m
            : (_isBasePayPreFunded ? Math.Max(0m, _totalRateMultiplier - 1.0m) : _totalRateMultiplier);

        _total += hourlyRate * workedHours * multiplier;
        return this;
    }

    public RestSpecialDayPayCalculator CalculateUnworkedPay(decimal hourlyRate, decimal unworkedHours)
    {
        if (unworkedHours <= 0 || !_isEligible) return this;

        // Eligible & Pre-Funded (Fixed): Base pay is already included in base salary, so delta is 0.0x
        // Eligible & Not Pre-Funded (Daily): Unworked rest day + special holiday credit is paid at 100% (1.0x) base rate
        var multiplier = _isBasePayPreFunded ? 0.0m : 1.0m;

        _total += hourlyRate * unworkedHours * multiplier;
        return this;
    }

    public decimal Total => _total;
}