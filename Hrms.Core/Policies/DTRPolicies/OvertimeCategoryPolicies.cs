namespace Hrms.Core.Policies.DTRPolicies;

internal abstract class SingleCategoryOTPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    protected SingleCategoryOTPolicy() : base(new IsEligibleForOvertime(), SpecFailBehaviour.ReturnInput) { }

    protected abstract double Hours(DailyRecordRunModel r);
    protected abstract IOtRateStrategy RateStrategy { get; }

    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var isOTEligible = new IsEligibleForOvertime().IsSatisfiedBy(context);
        if (!isOTEligible) return line;

        var hours = (decimal)Hours(context.DailyRecord);
        if (hours <= 0 || context.DailyRecord.ShiftWorkingHour <= 0) return line;

        var baseHourlyRate = PremiumRateHelper.GetHourlyRate(context);
        var basePayForHours = hours * baseHourlyRate;

        var (dayRate, fullRate) = RateStrategy.Resolve(context);

        // OTPremium is the pure OT variance above the base day rate -- when a client override
        // sets a flat OT rate lower than their own standard day rate, this is negative, which is
        // mathematically correct (their negotiated OT rate really is lower), just unusual to see
        // broken out as a "premium" on a report.
        line.Value += basePayForHours * fullRate;
        line.OTPremium += basePayForHours * (fullRate - dayRate);
        // line.NdPremium is intentionally not altered here because no ND hours exist in this pipeline category.
        return line;
    }
}

internal class RegularOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RegularOTHours;
    protected override IOtRateStrategy RateStrategy { get; } =
        new CompoundedOtRateStrategy(RateType.OVERTIME);
}

internal class RestDayOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDayOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new ClientOverrideOtRateStrategy(
        RateType.REST_DAY_OT,
        new CompoundedOtRateStrategy(RateType.RESTDAY_DUTY, RateType.HOLIDAY_OT));
}

internal class LegalHolOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.LegalHolOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new ClientOverrideOtRateStrategy(
        RateType.LEGAL_HOLIDAY_OT,
        new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT));
}

internal class RestLegalDayOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestLegalDayOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new ClientOverrideOtRateStrategy(
        RateType.REST_LEGAL_HOLIDAY_OT,
        new CompoundedOtRateStrategy(RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT));
}

internal class SpecialNonWorkingOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.SpecialHolOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new ClientOverrideOtRateStrategy(
        RateType.SPECIAL_HOLIDAY_OT,
        new CompoundedOtRateStrategy(RateType.SPECIAL_NON_WORKING, RateType.HOLIDAY_OT));
}

internal class RestSpecialDayOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestSpecialDayOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new ClientOverrideOtRateStrategy(
        RateType.REST_SPECIAL_HOLIDAY_OT,
        new CompoundedOtRateStrategy(RateType.RESTDAY_SPECIAL, RateType.HOLIDAY_OT));
}

internal class DoubleLegalOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.DoubleLegalOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new ClientOverrideOtRateStrategy(
        RateType.DOUBLE_LEGAL_HOLIDAY_OT,
        new CompoundedOtRateStrategy(RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT));
}

internal class RestDoubleLegalOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDoubleLegalOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new ClientOverrideOtRateStrategy(
        RateType.REST_DOUBLE_LEGAL_HOLIDAY_OT,
        new CompoundedOtRateStrategy(RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.HOLIDAY_OT));
}
