namespace Hrms.Core.Policies.DTRPolicies;

/// <summary>
/// Pays the "Rest Day + Double Legal Holiday" combo day — reads DailyRecord.RestDoubleLegalHours.
/// DOLE Standard:
/// - Unworked: 200% (2.0x daily rate)
/// - Worked: 390% (3.9x daily rate for regular 8 hours)
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

        var hourlyRate = RateHelper.GetHourlyRate(context);
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
    private const decimal DOLE_WORKED_MULTIPLIER = 3.9m; // 300% double holiday + 30% rest day premium
    private const decimal DOLE_UNWORKED_MULTIPLIER = 2.0m; // 200% double regular holiday

    private RestDoubleLegalPayCalculator(PayrollContext context)
    {
        _isEligible = new IsEligibleForHolidayPay().IsSatisfiedBy(context);
        _isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                              context.Employee.IsRestDayPaid &&
                              context.Employee.IsRegularHolidayIncluded;
    }

    public static RestDoubleLegalPayCalculator ForContext(PayrollContext context) => new(context);

    public RestDoubleLegalPayCalculator CalculateWorkedPay(decimal hourlyRate, decimal workedHours)
    {
        if (workedHours <= 0) return this;

        // DOLE Worked Rest Day + Double Holiday: 390% (3.9x)
        // If ineligible: Earns 1.0x standard hourly rate
        // If eligible & pre-funded: Earns 2.9x additional premium (3.9x - 1.0x pre-funded base)
        // If eligible & not pre-funded: Earns full 3.9x
        var multiplier = !_isEligible
            ? 1.0m
            : (_isBasePayPreFunded ? (DOLE_WORKED_MULTIPLIER - 1.0m) : DOLE_WORKED_MULTIPLIER);

        _total += hourlyRate * workedHours * multiplier;
        return this;
    }

    public RestDoubleLegalPayCalculator CalculateUnworkedPay(decimal hourlyRate, decimal unworkedHours)
    {
        if (unworkedHours <= 0 || !_isEligible) return this;

        // DOLE Unworked Double Holiday: 200% (2.0x)
        // If eligible & pre-funded (fixed monthly): Base pay (2.0x) is already included in base pay, so delta is 0.0x
        // If eligible & not pre-funded (daily-paid): Earns full 2.0x
        var multiplier = _isBasePayPreFunded ? 0.0m : DOLE_UNWORKED_MULTIPLIER;

        _total += hourlyRate * unworkedHours * multiplier;
        return this;
    }

    public decimal Total => _total;
}