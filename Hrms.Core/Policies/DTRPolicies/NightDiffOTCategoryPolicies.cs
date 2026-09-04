namespace Hrms.Core.Policies.DTRPolicies;

internal abstract class SingleCategoryNDOTPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    protected SingleCategoryNDOTPolicy() : base(new IsEligibleForNightDifferential(), SpecFailBehaviour.ReturnInput) { }

    protected abstract double Hours(DailyRecordRunModel r);
    protected abstract RateType[] PolicyRateTypes { get; }
     
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var hours = (decimal)Hours(context.DailyRecord);
        if (hours <= 0 || context.DailyRecord.ShiftWorkingHour <= 0) return line;

        var baseHourlyRate = PremiumRateHelper.GetHourlyRate(context);
        var basePayForHours = hours * baseHourlyRate;

        // 1. Establish the Day-Type Base Multiplier (e.g., Regular = 1.0, RestDay = 1.3, Legal = 2.0)
        decimal dayTypeMultiplier = 1.0m;
        foreach (var rateType in PolicyRateTypes)
        {
            if (rateType != RateType.HOLIDAY_OT && rateType != RateType.OVERTIME && rateType != RateType.NIGHTDIFF)
            {
                // Multiplies out compound day bases like Rest Day + Legal Day (1.30 * 2.00 = 2.60)
                dayTypeMultiplier *= PremiumRateHelper.GetRate(context, rateType, 1.0m);
            }
        }

        // 2. Fetch the standard operational premiums from your helper
        // Typically: OVERTIME/HOLIDAY_OT = 1.25, NIGHTDIFF = 1.10
        var otRateMultiplier = PolicyRateTypes.Contains(RateType.HOLIDAY_OT) || PolicyRateTypes.Contains(RateType.OVERTIME)
            ? PremiumRateHelper.GetRate(context, PolicyRateTypes.Contains(RateType.HOLIDAY_OT) ? RateType.HOLIDAY_OT : RateType.OVERTIME, 1.25m)
            : 1.0m;

        var ndRateMultiplier = PolicyRateTypes.Contains(RateType.NIGHTDIFF)
            ? PremiumRateHelper.GetRate(context, RateType.NIGHTDIFF, 1.10m)
            : 1.0m;

        // 3. Compute tiered compounding multipliers dynamically
        decimal coreDayRate = dayTypeMultiplier;                         // Tier 1: Day rate (e.g., 1.30)
        decimal overtimeRate = coreDayRate * otRateMultiplier;           // Tier 2: Day rate + OT (e.g., 1.30 * 1.25 = 1.625)
        decimal fullyCompoundedRate = overtimeRate * ndRateMultiplier;    // Tier 3: Day rate + OT + ND (e.g., 1.625 * 1.10 = 1.7875)

        // 4. Extract pure premiums by calculating the differences between mathematical tiers
        decimal pureOtPremiumMultiplier = overtimeRate - coreDayRate;
        decimal pureNdPremiumMultiplier = fullyCompoundedRate - overtimeRate;

        // Night differential is always paid in full — no Fixed-salary or per-employee
        // "already included in the monthly rate" discount applies here.
        // 5. Allocate values cleanly to the tracking pipeline instance
        line.Value += basePayForHours * fullyCompoundedRate;
        line.OTPremium += basePayForHours * pureOtPremiumMultiplier;
        line.NDPremium += basePayForHours * pureNdPremiumMultiplier;
        return line;
    }
}

internal class RegularNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RegularNDOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.OVERTIME, RateType.NIGHTDIFF };
}

internal class RestDayNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDayNDOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.RESTDAY_DUTY, RateType.HOLIDAY_OT, RateType.NIGHTDIFF };
}
internal class LegalHolNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.LegalHolNightDiffOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT, RateType.NIGHTDIFF };
}

internal class RestLegalDayNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestLegalDayNDOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT, RateType.NIGHTDIFF };
}

internal class SpecialNonWorkingNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.SpecialHolNightDiffOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.SPECIAL_NON_WORKING, RateType.HOLIDAY_OT, RateType.NIGHTDIFF };
}

internal class RestSpecialDayNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestSpecialDayNDOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.RESTDAY_SPECIAL, RateType.HOLIDAY_OT, RateType.NIGHTDIFF };
}
internal class DoubleLegalNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.DoubleLegalNDOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT, RateType.NIGHTDIFF };
}
internal class RestDoubleLegalNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDoubleLegalNDOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT, RateType.NIGHTDIFF };
}