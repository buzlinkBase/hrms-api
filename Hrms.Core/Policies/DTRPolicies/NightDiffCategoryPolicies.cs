namespace Hrms.Core.Policies.DTRPolicies;

internal abstract class SingleCategoryNDPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    protected SingleCategoryNDPolicy() : base(new IsEligibleForNightDifferential(), SpecFailBehaviour.ReturnInput) { }

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
            if (rateType != RateType.NIGHTDIFF)
            {
                // Multiplies out compound day bases like Rest Day + Legal Day (1.30 * 2.00 = 2.60)
                dayTypeMultiplier *= PremiumRateHelper.GetRate(context, rateType, RATE_DEFAULT.For(rateType));
            }
        }

        // 2. Fetch the standard operational Night Differential multiplier from your helper
        // Typically defaults to 1.10 (10% premium)
        var ndRateMultiplier = PolicyRateTypes.Contains(RateType.NIGHTDIFF)
            ? PremiumRateHelper.GetRate(context, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
            : 1.0m;

        // Setup > Company Policy > OT/ND Calculation Method. Compounded (default, DOLE-standard):
        // day x ND, multiplicatively. Additive (opt-in, company-wide only): day + (ND - 1) --
        // computes LESS than Compounded for these hours on any category whose day rate isn't
        // 1.0. See NightDiffOTCategoryPolicies for the OT+ND combo, and OtNdCalculationMethod's
        // own doc comment.
        var isAdditive = context.Payload.CompanyPolicy.OtNdCalculationMethod == OtNdCalculationMethod.Additive;

        // 3. Compute tiered compounding multipliers dynamically
        decimal coreDayRate = dayTypeMultiplier;                         // Tier 1: Basic day rate (e.g., 1.30)
        decimal fullyCompoundedRate = isAdditive
            ? coreDayRate + (ndRateMultiplier - 1.0m)                     // Tier 2 (Additive): 1.30 + 0.10 = 1.40
            : coreDayRate * ndRateMultiplier;                             // Tier 2 (Compounded): 1.30 * 1.10 = 1.43

        // 4. Extract pure premiums by calculating the differences between mathematical tiers
        // Since this is pure ND, OtPremium remains untouched (0), and ND captures the exact variance.
        decimal pureNdPremiumMultiplier = fullyCompoundedRate - coreDayRate;

        // Night differential is always paid in full — no Fixed-salary or per-employee
        // "already included in the monthly rate" discount applies here.
        // 5. Allocate values cleanly to the tracking pipeline instance
        line.Value += basePayForHours * fullyCompoundedRate;
        line.NDPremium += basePayForHours * pureNdPremiumMultiplier;
        // line.OtPremium is intentionally not altered here because no OT hours exist in this pipeline category.

        // Flat premium -- just the (ndRateMultiplier - 1) fraction against the plain base pay
        // for these hours, e.g. a 1.10 NIGHTDIFF rate contributes only the .10 -- NOT scaled by
        // the day-type multiplier the way pureNdPremiumMultiplier above is. This is the segregated
        // "ND Premium" figure recorded per category (see BasicPayrollCalculator.Calculate), kept
        // separate from NDPremium so existing consumers of NDPremium/NDPremiumPay are unaffected.
        line.FlatNightDiffPremium += basePayForHours * (ndRateMultiplier - 1.0m);
        return line;
    }
}

internal class RegularNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RegularNDHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.NIGHTDIFF };
}

internal class RestDayNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDayNDHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.RESTDAY_DUTY, RateType.NIGHTDIFF };
}

internal class LegalHolNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.LegalHolNightDiffHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.LEGAL_HOLIDAY_DUTY, RateType.NIGHTDIFF };
}

internal class RestLegalDayNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestLegalDayNDHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.NIGHTDIFF };
}

internal class SpecialNonWorkingNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.SpecialHolNightDiffHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.SPECIAL_NON_WORKING, RateType.NIGHTDIFF };
}

internal class RestSpecialDayNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestSpecialDayNDHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.RESTDAY_SPECIAL, RateType.NIGHTDIFF };
}

internal class DoubleLegalNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.DoubleLegalNDHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.NIGHTDIFF };
}

internal class RestDoubleLegalNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDoubleLegalNDHours;
    protected override RateType[] PolicyRateTypes => new[] { RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.NIGHTDIFF };
}