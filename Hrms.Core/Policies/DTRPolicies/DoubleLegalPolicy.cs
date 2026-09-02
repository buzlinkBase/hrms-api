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
        //TODO must consider Holiday Hours Basis
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.WorkTypeEnum is not (WorkType.DoubleLegal or WorkType.DoubleLegalDuty) ||
            dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.HolCount == 0)
        {
            return line;
        }

        var hourlyRate = RateHelper.GetHourlyRate(context);
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
    private const decimal DOLE_WORKED_MULTIPLIER = 3.0m;
    private const decimal DOLE_UNWORKED_MULTIPLIER = 2.0m;

    private DoubleLegalPayCalculator(PayrollContext context)
    {
        _isEligible = new IsEligibleForRegularHolidayPay().IsSatisfiedBy(context);
        _isBasePayPreFunded = context.Employee.SalaryType == SalaryType.FIXED &&
                              context.Employee.IsRegularHolidayIncluded;
    }

    public static DoubleLegalPayCalculator ForContext(PayrollContext context) => new(context);

    public DoubleLegalPayCalculator CalculateWorkedPay(decimal hourlyRate, decimal workedHours)
    {
        if (workedHours <= 0) return this;

        // DOLE Worked Double Holiday: 300% (3.0x)
        // If ineligible: Earns 1.0x basic hourly rate
        // If eligible & pre-funded (fixed monthly): Earns 2.0x additional premium (3.0 - 1.0 pre-funded)
        // If eligible & not pre-funded (daily): Earns full 3.0x

        var multiplier = !_isEligible
            ? 1.0m
            : (_isBasePayPreFunded ? (DOLE_WORKED_MULTIPLIER - 1.0m) : DOLE_WORKED_MULTIPLIER);

        _total += hourlyRate * workedHours * multiplier;
        return this;
    }

    public DoubleLegalPayCalculator CalculateUnworkedPay(decimal hourlyRate, decimal unworkedHours)
    {
        if (unworkedHours <= 0 || !_isEligible) return this;
        // DOLE Unworked Double Holiday: 200% (2.0x)
        // If eligible & pre-funded (fixed monthly): Earns 1.0x additional premium (2.0 - 1.0 pre-funded)
        // If eligible & not pre-funded (daily): Earns full 2.0x
        var multiplier = _isBasePayPreFunded ? (DOLE_UNWORKED_MULTIPLIER - 1.0m) : DOLE_UNWORKED_MULTIPLIER;
        _total += hourlyRate * unworkedHours * multiplier;
        return this;
    }
    public decimal Total => _total;
}