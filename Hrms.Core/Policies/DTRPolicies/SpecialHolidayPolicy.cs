namespace Hrms.Core.Policies.DTRPolicies;

public delegate bool RateCondition(PayrollContext context);
public delegate decimal RateCalc(PayrollContext context);
//public delegate decimal ColumnProvider(PayrollContext context);

public class SpecialHolidayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var premiumRate = SpecialHolRateSolver.ResolvePremiumRate(context);
        var dailyRate = context.Employee.DailyRate;
        var totalDay = (decimal)context.DailyRecord.SpecialHolHours / (decimal)context.DailyRecord.ShiftWorkingHour;
        line.Value += totalDay * dailyRate * premiumRate;
        return line;
    }

}

public static class SpecialHolRateSolver
{
    private static readonly List<RateRule> _rules = new()
    {
        // Special Working Holiday Duty
        new RateRule
        {
            Condition = ctx => new IsSpecialHolidayDuty().IsSatisfiedBy(ctx),
            GetRate = ctx => ctx.Payload.PremiumRates.TryGetValue(RateType.SPECIAL_WORKING, out var r) && r != 0 ? r :  RATE_DEFAULT.SPECIAL_WORKING
        }, 
        // Special Non-Working Holiday
        new RateRule
        {
            Condition = ctx => new IsSpecialNonWorkingDuty().IsSatisfiedBy(ctx),
            GetRate = ctx => ctx.Payload.PremiumRates.TryGetValue(RateType.SPECIAL_NON_WORKING, out var r) && r != 0 ? r :RATE_DEFAULT.SPECIAL_NON_WORKING
        },
        // Rest Day + Special Holiday
        new RateRule
        {
            Condition = ctx => new IsRestDaySpecialHolidayDuty().IsSatisfiedBy(ctx),
            GetRate = ctx => ctx.Payload.PremiumRates.TryGetValue(RateType.RESTDAY_SPECIAL, out var r) && r != 0 ? r : RATE_DEFAULT.RESTDAY_SPECIAL
        },
        //new RateRule
        //{
        //    Condition = ctx => new IsSpecialHoliday().IsSatisfiedBy(ctx),
        //    GetRate = ctx => 0,
        //},
    };

    public static decimal ResolvePremiumRate(PayrollContext context)
    {
        foreach (var rule in _rules)
        {
            if (rule.Condition(context))
                return rule.GetRate(context);
        }
        return 1.00m; // default multiplier (no premium)
    }
}
public class RateRule
{
    public RateCondition Condition { get; init; }
    public RateCalc GetRate { get; init; }
}