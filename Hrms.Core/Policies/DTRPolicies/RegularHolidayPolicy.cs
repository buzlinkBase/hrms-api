
namespace Hrms.Core.Policies.DTRPolicies;
public class RegularHolidayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        //TODO must consider Holiday Hours Basis
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.WorkTypeEnum is not (WorkType.LegalHoliday or WorkType.LegalHolidayDuty) ||
            dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.HolCount == 0)
        {
            return line;
        }

        var hourlyRate = RateHelper.GetHourlyRate(context);
        var workedHours = (decimal)Math.Max(0, dailyRecord.LegalHolHours);
        var unworkedHours = Math.Max(0m, (decimal)dailyRecord.ShiftWorkingHour - workedHours);

        var earnings = HolidayPayCalculator
            .ForContext(context, line)
            .CalculateWorkedPay(hourlyRate, workedHours)
            .CalculateUnworkedPay(hourlyRate, unworkedHours)
            .Total;

        line.Value += earnings;
        return line;
    }
}

public class HolidayPayCalculator
{
    private decimal _total;
    private readonly bool _isEligible;
    private readonly bool _isBasePayPreFunded;
    private readonly decimal _totalRateMultiplier;
    private readonly BasicPipelineData _line;
    private HolidayPayCalculator(PayrollContext context, BasicPipelineData line)
    {
        _isEligible = new IsEligibleForRegularHolidayPay().IsSatisfiedBy(context);
        _isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                              context.Employee.IsRegularHolidayIncluded;
        _totalRateMultiplier = PremiumRateHelper.GetRate(context, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY);
        _line = line;
    }
    public static HolidayPayCalculator ForContext(PayrollContext context, BasicPipelineData line) => new(context, line);
    public HolidayPayCalculator CalculateWorkedPay(decimal hourlyRate, decimal workedHours)
    {
        if (workedHours <= 0) return this;

        // Ineligible: Earns 1.0x standard hourly rate
        // Eligible & Pre-Funded (Fixed): Earns delta premium above 1.0x base pay (e.g., 2.0x - 1.0x = 1.0x)
        // Eligible & Not Pre-Funded (Daily): Earns full configured worked rate multiplier (2.0x)
        var multiplier = !_isEligible
            ? 1.0m
            : (_isBasePayPreFunded
                ? Math.Max(0m, _totalRateMultiplier - 1.0m)
                : _totalRateMultiplier);

        _total += hourlyRate * workedHours * multiplier;
        _line.Worked = _total;
        return this;
    }

    public HolidayPayCalculator CalculateUnworkedPay(decimal hourlyRate, decimal unworkedHours)
    {
        if (unworkedHours <= 0 || !_isEligible) return this;

        // Eligible & Pre-Funded (Fixed): Unworked hours base pay (1.0x) is already included in monthly base salary, so delta is 0
        // Eligible & Not Pre-Funded (Daily): Unworked regular holiday hours receive 100% (1.0x) base rate
        var multiplier = _isBasePayPreFunded ? 0.0m : 1.0m;
        _total += hourlyRate * unworkedHours * multiplier;
        _line.UnWork = _total;
        return this;
    }

    public decimal Total => _total;
}