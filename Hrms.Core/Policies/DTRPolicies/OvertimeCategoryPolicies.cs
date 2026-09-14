namespace Hrms.Core.Policies.DTRPolicies;

internal abstract class SingleCategoryOTPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    protected SingleCategoryOTPolicy() : base(new IsEligibleForOvertime(), SpecFailBehaviour.ReturnInput) { }

    protected abstract double Hours(DailyRecordRunModel r);
    protected abstract RateType[] PolicyRateTypes { get; }

    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var isOTEligible = new IsEligibleForOvertime().IsSatisfiedBy(context);
        if (!isOTEligible) return line;

        var hours = (decimal)Hours(context.DailyRecord);
        if (hours <= 0 || context.DailyRecord.ShiftWorkingHour <= 0) return line;

        var baseHourlyRate = PremiumRateHelper.GetHourlyRate(context);
        var basePayForHours = hours * baseHourlyRate;

        // 1. Establish the Day-Type Base Multiplier (e.g., Regular = 1.0, RestDay = 1.3, Legal = 2.0)
        decimal dayTypeMultiplier = 1.0m;
        foreach (var rateType in PolicyRateTypes)
        {
            if (rateType != RateType.HOLIDAY_OT && rateType != RateType.OVERTIME)
            {
                // Multiplies out compound day bases like Rest Day + Legal Day (1.30 * 2.00 = 2.60)
                dayTypeMultiplier *= PremiumRateHelper.GetRate(context, rateType, 1.0m);
            }
        }

        // 2. Fetch the standard operational Overtime multiplier from your helper
        // Typically defaults to 1.25 (25% premium)
        var otRateMultiplier = PolicyRateTypes.Contains(RateType.HOLIDAY_OT) || PolicyRateTypes.Contains(RateType.OVERTIME)
            ? PremiumRateHelper.GetRate(context, PolicyRateTypes.Contains(RateType.HOLIDAY_OT) ? RateType.HOLIDAY_OT : RateType.OVERTIME, 1.25m)
            : 1.0m;

        // 3. Compute tiered compounding multipliers dynamically
        decimal coreDayRate = dayTypeMultiplier;                         // Tier 1: Basic day rate (e.g., 2.60 for Rest Legal)
        decimal fullyCompoundedRate = coreDayRate * otRateMultiplier;    // Tier 2: Day rate + OT (e.g., 2.60 * 1.30 = 3.38)

        // 4. Extract pure premiums by calculating the differences between mathematical tiers
        // Since this is pure OT, NdPremium remains untouched (0), and OT captures the exact variance.
        decimal pureOtPremiumMultiplier = fullyCompoundedRate - coreDayRate;

        // 5. Apply the effective multiplier to gross value
        decimal effectiveTotalMultiplier = fullyCompoundedRate;

        // 6. Allocate values cleanly to the tracking pipeline instance
        line.Value += basePayForHours * effectiveTotalMultiplier;
        line.OTPremium += basePayForHours * pureOtPremiumMultiplier;
        // line.NdPremium is intentionally not altered here because no ND hours exist in this pipeline category.
        return line;
    } 
}

internal class RegularOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RegularOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.OVERTIME };
}

internal class RestDayOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDayOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.RESTDAY_DUTY, RateType.HOLIDAY_OT };
}

internal class LegalHolOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.LegalHolOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT };
}

internal class RestLegalDayOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestLegalDayOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT };
}

internal class SpecialNonWorkingOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.SpecialHolOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.SPECIAL_NON_WORKING, RateType.HOLIDAY_OT };
}

internal class RestSpecialDayOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestSpecialDayOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.RESTDAY_SPECIAL, RateType.HOLIDAY_OT };
}

internal class DoubleLegalOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.DoubleLegalOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT };
}

internal class RestDoubleLegalOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDoubleLegalOTHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT };
}