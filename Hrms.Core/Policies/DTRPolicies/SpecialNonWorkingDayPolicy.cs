namespace Hrms.Core.Policies.DTRPolicies;

internal class SpecialNonWorkingDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.WorkTypeEnum is not (WorkType.SpecialNonWorkingHoliday 
                or WorkType.SpecialHolidayDuty) ||
            dailyRecord.ShiftWorkingHour <= 0)
        {
            return line;
        }

        var hourlyRate = RateHelper.GetHourlyRate(context);
        var workedHours = (decimal)Math.Max(0, dailyRecord.SpecialHolHours);
        var unworkedHours = Math.Max(0m, (decimal)dailyRecord.ShiftWorkingHour - workedHours);

        var earnings = SpecialHolidayPayCalculator
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
        _isEligible = new IsEligibleForSpecialHolidayPay().IsSatisfiedBy(context);
        _isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                              context.Employee.IsSpecialNonWorkingIncluded;
        _totalRateMultiplier = PremiumRateHelper.GetRate(context, RateType.SPECIAL_NON_WORKING, RATE_DEFAULT.SPECIAL_NON_WORKING);
    }

    public static SpecialHolidayPayCalculator ForContext(PayrollContext context) => new(context);

    public SpecialHolidayPayCalculator CalculateWorkedPay(decimal hourlyRate, decimal workedHours)
    {
        if (workedHours <= 0) return this;

        // Ineligible: Earns 1.0x standard hourly rate
        // Eligible & Pre-Funded (Fixed Salary): Earns delta premium above 1.0x base pay (e.g., 1.30 - 1.00 = 0.30)
        // Eligible & Not Pre-Funded (Daily Salary): Earns full configured special holiday rate multiplier (1.30x)
        var multiplier = !_isEligible
            ? 1.0m
            : (_isBasePayPreFunded ? Math.Max(0m, _totalRateMultiplier - 1.0m) : _totalRateMultiplier);

        _total += hourlyRate * workedHours * multiplier;
        return this;
    }

    public SpecialHolidayPayCalculator CalculateUnworkedPay(decimal hourlyRate, decimal unworkedHours)
    {
        if (unworkedHours <= 0 || !_isEligible) return this;

        // DOLE Standard: "No Work, No Pay" applies to Special Non-Working Holidays.
        // Unworked hours receive 0.0x unless pre-funded base pay covers it via monthly salary.
        var multiplier = 0.0m;

        _total += hourlyRate * unworkedHours * multiplier;
        return this;
    }

    public decimal Total => _total;
}