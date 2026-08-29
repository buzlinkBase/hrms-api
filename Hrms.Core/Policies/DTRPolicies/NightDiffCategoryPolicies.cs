namespace Hrms.Core.Policies.DTRPolicies;

internal abstract class SingleCategoryNDPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    protected SingleCategoryNDPolicy() : base(new IsEligibleForNightDifferential(), SpecFailBehaviour.ReturnInput) { }

    protected abstract double Hours(DailyRecordRunModel r);
    protected abstract RateType[] PolicyRateTypes { get; }

    //public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    //{
    //    var hours = (decimal)Hours(context.DailyRecord);
    //    if (hours <= 0 || context.DailyRecord.ShiftWorkingHour <= 0) return line;

    //    var baseHourlyRate = PremiumRateHelper.GetHourlyRate(context);

    //    decimal combinedRateMultiplier = 1.0m;
    //    foreach (var rateType in PolicyRateTypes)
    //    {
    //        var rate = PremiumRateHelper.GetRate(context, rateType, 1.0m);
    //        combinedRateMultiplier *= rate;
    //    }

    //    var isPreFunded = context.Employee.IsNightDiffIncluded ||
    //                      context.Employee.SalaryType == SalaryType.FIXED;

    //    var effectiveMultiplier = isPreFunded
    //        ? Math.Max(0m, combinedRateMultiplier - 1.00m)
    //        : combinedRateMultiplier;

    //    line.Value += hours * baseHourlyRate * effectiveMultiplier;
    //    return line;
    //}
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
                dayTypeMultiplier *= PremiumRateHelper.GetRate(context, rateType, 1.0m);
            }
        }

        // 2. Fetch the standard operational Night Differential multiplier from your helper
        // Typically defaults to 1.10 (10% premium)
        var ndRateMultiplier = PolicyRateTypes.Contains(RateType.NIGHTDIFF)
            ? PremiumRateHelper.GetRate(context, RateType.NIGHTDIFF, 1.10m)
            : 1.0m;

        // 3. Compute tiered compounding multipliers dynamically
        decimal coreDayRate = dayTypeMultiplier;                         // Tier 1: Basic day rate (e.g., 1.30)
        decimal fullyCompoundedRate = coreDayRate * ndRateMultiplier;    // Tier 2: Day rate + ND (e.g., 1.30 * 1.10 = 1.43)

        // 4. Extract pure premiums by calculating the differences between mathematical tiers
        // Since this is pure ND, OtPremium remains untouched (0), and ND captures the exact variance.
        decimal pureNdPremiumMultiplier = fullyCompoundedRate - coreDayRate;

        // 5. Handle Pre-Funded / Fixed salary configurations gracefully
        var isPreFunded = context.Employee.IsNightDiffIncluded ||
                          context.Employee.SalaryType == SalaryType.FIXED;

        decimal effectiveTotalMultiplier = isPreFunded
            ? Math.Max(0m, fullyCompoundedRate - 1.00m)
            : fullyCompoundedRate;

        // 6. Allocate values cleanly to the tracking pipeline instance
        line.Value += basePayForHours * effectiveTotalMultiplier;
        line.NDPremium += basePayForHours * pureNdPremiumMultiplier;
        // line.OtPremium is intentionally not altered here because no OT hours exist in this pipeline category.
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