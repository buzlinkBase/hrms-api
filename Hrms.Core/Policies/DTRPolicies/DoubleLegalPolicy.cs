namespace Hrms.Core.Policies.DTRPolicies;

/// <summary>
/// Pays the "Double Legal Holiday" day (two regular holidays coinciding on the same
/// calendar date) — reads DailyRecord.DoubleLegalHours. Mirrors RegularHolidayPolicy's
/// WorkType-gated worked/unworked split and eligibility handling. Multiplier is double the
/// configured Legal Holiday Duty rate rather than a separate hardcoded constant, so it stays
/// in sync with whatever the admin configures for RateType.LEGAL_HOLIDAY_DUTY.
/// </summary>
internal class DoubleLegalPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;

        if (dailyRecord.WorkTypeEnum is not (WorkType.DoubleLegal or WorkType.DoubleLegalDuty) ||
            dailyRecord.ShiftWorkingHour <= 0)
        {
            return line;
        }

        var hourlyRate = context.Employee.DailyRate / (decimal)dailyRecord.ShiftWorkingHour;
        var workedHours = (decimal)Math.Max(0, dailyRecord.DoubleLegalHours);
        var unworkedHours = Math.Max(0m, (decimal)dailyRecord.ShiftWorkingHour - workedHours);

        var earnings = DoubleLegalPayCalculator
            .ForContext(context)
            .CalculateWorkedPay(hourlyRate, workedHours)
            .CalculateUnworkedPay(hourlyRate, unworkedHours)
            .Total;

        line.Value += earnings;
        return line;
    }
}

public class DoubleLegalPayCalculator
{
    private decimal _total;
    private readonly bool _isEligible;
    private readonly bool _isBasePayPreFunded;
    private readonly decimal _totalRateMultiplier;

    private DoubleLegalPayCalculator(PayrollContext context)
    {
        _isEligible = new IsEligibleForHolidayPay().IsSatisfiedBy(context);
        _isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                              context.Employee.IsRegularHolidayIncluded;
        _totalRateMultiplier = Math.Max(2m, context.DailyRecord.HolCount) * PremiumRateHelper.GetRate(context, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY);
    }

    public static DoubleLegalPayCalculator ForContext(PayrollContext context) => new(context);

    public DoubleLegalPayCalculator CalculateWorkedPay(decimal hourlyRate, decimal workedHours)
    {
        if (workedHours <= 0) return this;
        // Ineligible: Earns 1.0x standard rate
        // Eligible & Pre-Funded: Earns delta premium above 1.0 (e.g., 4.00 - 1.00 = 3.00)
        // Eligible & Not Pre-Funded: Earns full configured double-legal multiplier (e.g., 4.00)
        var multiplier = !_isEligible
            ? 1.0m
            : (_isBasePayPreFunded ? Math.Max(0m, _totalRateMultiplier - 1.0m) : _totalRateMultiplier);

        _total += hourlyRate * workedHours * multiplier;
        return this;
    }

    public DoubleLegalPayCalculator CalculateUnworkedPay(decimal hourlyRate, decimal unworkedHours)
    {
        if (unworkedHours <= 0 || !_isEligible || _isBasePayPreFunded) return this;

        // Unworked double legal holiday credit is paid at 100% (1.0x) base rate
        _total += hourlyRate * unworkedHours * 1.0m;
        return this;
    }

    public decimal Total => _total;
}
