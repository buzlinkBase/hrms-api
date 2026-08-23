namespace Hrms.Core.Policies.DTRPolicies;

 
internal class SpecialWorkDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.WorkTypeEnum is not (WorkType.SpecialNonWorkingHoliday or WorkType.SpecialHolidayDuty) ||
            dailyRecord.ShiftWorkingHour <= 0)
        {
            return line;
        }

        var hourlyRate = context.Employee.DailyRate / (decimal)dailyRecord.ShiftWorkingHour;
        var workedHours = (decimal)Math.Max(0, dailyRecord.SpecialHolHours);
        var unworkedHours = Math.Max(0m, (decimal)dailyRecord.ShiftWorkingHour - workedHours);

        var earnings = HolidayPayCalculator
            .ForContext(context)
            .CalculateWorkedPay(hourlyRate, workedHours)
            .CalculateUnworkedPay(hourlyRate, unworkedHours)
            .Total;

        line.Value += earnings;
        return line;
    }
}

public class SpecialHolidayPayCalculator
{
    private decimal _total;
    private readonly bool _isEligible;
    private readonly bool _isBasePayPreFunded;
    private readonly decimal _totalRateMultiplier;

    private SpecialHolidayPayCalculator(PayrollContext context)
    {
        _isEligible = new IsEligibleForHolidayPay().IsSatisfiedBy(context);
        _isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                              context.Employee.IsSpecialNonWorkingIncluded;
        _totalRateMultiplier = PremiumRateHelper.GetRate(context, RateType.SPECIAL_NON_WORKING, RATE_DEFAULT.SPECIAL_NON_WORKING);
    }

    public static SpecialHolidayPayCalculator ForContext(PayrollContext context) => new(context);

    public SpecialHolidayPayCalculator CalculateWorkedPay(decimal hourlyRate, decimal workedHours)
    {
        if (workedHours <= 0) return this;
        // Ineligible: Earns 1.0x standard rate
        // Eligible & Pre-Funded: Earns delta premium above 1.0 (e.g., 2.60 - 1.00 = 1.60)
        // Eligible & Not Pre-Funded: Earns full configured rate multiplier (e.g., 2.60)
        var multiplier = !_isEligible
            ? 1.0m
            : (_isBasePayPreFunded ? Math.Max(0m, _totalRateMultiplier - 1.0m) : _totalRateMultiplier);

        _total += hourlyRate * workedHours * multiplier;
        return this;
    }

    public SpecialHolidayPayCalculator CalculateUnworkedPay(decimal hourlyRate, decimal unworkedHours)
    {
        if (unworkedHours <= 0 || !_isEligible || _isBasePayPreFunded) return this;

        // Unworked regular holiday pay is paid at 100% (1.0x) base rate
        _total += hourlyRate * unworkedHours * 1.0m;
        return this;
    }

    public decimal Total => _total;
}