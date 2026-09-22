namespace Hrms.Core.Policies.DTRPolicies;

internal abstract class SingleCategoryNDOTPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    protected SingleCategoryNDOTPolicy() : base(new IsEligibleForNightDifferential(), SpecFailBehaviour.ReturnInput) { }

    protected abstract double Hours(DailyRecordRunModel r);
    // Same strategy instance shape as SingleCategoryOTPolicy (OvertimeCategoryPolicies.cs) for
    // the same category -- guarantees a category's plain-OT and NDOT hours can never resolve to
    // different effective OT rates, whether or not a client override is configured. See
    // CompoundedOtRateStrategy.ResolveRawOtRate.
    protected abstract IOtRateStrategy RateStrategy { get; }

    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var hours = (decimal)Hours(context.DailyRecord);
        if (hours <= 0 || context.DailyRecord.ShiftWorkingHour <= 0) return line;

        var baseHourlyRate = PremiumRateHelper.GetHourlyRate(context);
        var basePayForHours = hours * baseHourlyRate;

        // 1. Day-Type Base Multiplier (e.g., Regular = 1.0, RestDay = 1.3, Legal = 2.0) and the
        // OT-tier multiplier (override-aware) both come from the category's own RateStrategy.
        var (dayTypeMultiplier, _) = RateStrategy.Resolve(context);
        var otRateMultiplier = RateStrategy.ResolveRawOtRate(context);

        // Night differential itself was never part of any OT override.
        var ndRateMultiplier = PremiumRateHelper.GetRate(context, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);

        // Setup > Company Policy > OT/ND Calculation Method. Compounded (default, DOLE-standard):
        // day x OT x ND, multiplicatively. Additive (opt-in, company-wide only): the day-type
        // multiplier is DROPPED entirely for hours that are OT -- only the raw OT rate (plus the
        // ND delta, if also ND) applies, i.e. otRate + (ND - 1). Confirmed against the company's
        // own manual payroll worksheet: OT/NDOT hours price at the flat OT rate alone, not
        // day-rate x OT-rate. See OtNdCalculationMethod's own doc comment.
        var isAdditive = context.Payload.CompanyPolicy.OtNdCalculationMethod == OtNdCalculationMethod.Additive;

        // 2. Compute tiered multipliers dynamically. "Tier 2"/"Tier 3" mean different things
        // depending on mode, but pureOtPremiumMultiplier/pureNdPremiumMultiplier below are always
        // just "the difference between consecutive tiers", so OTPremium + NDPremium + day-rate
        // telescopes back to Value in either mode.
        decimal coreDayRate = isAdditive ? 0m : dayTypeMultiplier;        // Tier 1 (Additive): dropped entirely
        decimal overtimeRate = isAdditive
            ? otRateMultiplier                                            // Tier 2 (Additive): 1.25 (day rate not applied)
            : coreDayRate * otRateMultiplier;                             // Tier 2 (Compounded): 1.30 * 1.25 = 1.625
        decimal fullyCompoundedRate = isAdditive
            ? overtimeRate + (ndRateMultiplier - 1.0m)                    // Tier 3 (Additive): 1.25 + 0.10 = 1.35
            : overtimeRate * ndRateMultiplier;                            // Tier 3 (Compounded): 1.625 * 1.10 = 1.7875

        // 3. Extract pure premiums by calculating the differences between mathematical tiers
        decimal pureOtPremiumMultiplier = overtimeRate - coreDayRate;
        decimal pureNdPremiumMultiplier = fullyCompoundedRate - overtimeRate;

        // Night differential is always paid in full — no Fixed-salary or per-employee
        // "already included in the monthly rate" discount applies here.
        // 4. Allocate values cleanly to the tracking pipeline instance
        line.Value += basePayForHours * fullyCompoundedRate;
        line.OTPremium += basePayForHours * pureOtPremiumMultiplier;
        line.NDPremium += basePayForHours * pureNdPremiumMultiplier;

        // Segregated recording -- NOT scaled by the other tiers the way pureOtPremiumMultiplier/
        // pureNdPremiumMultiplier above are. FlatOvertimeBase is always the raw OT rate ALONE
        // (hours * otRateMultiplier), matching the plain OT policy's own FlatOvertimeBase and the
        // client-approved "hours x one clean rate" Hours-format payslip presentation -- this is
        // mode-independent because Additive mode's Value formula above already drops the
        // day-type multiplier for these hours entirely, so the raw OT rate alone IS the whole
        // OT-bucket contribution under Additive mode (no separate day-rate portion to net out).
        // FlatNightDiffPremium (the (rate - 1) fraction against the plain base pay) is likewise
        // unaffected by this setting in either mode.
        line.FlatOvertimeBase += basePayForHours * otRateMultiplier;
        line.FlatNightDiffPremium += basePayForHours * (ndRateMultiplier - 1.0m);
        return line;
    }
}

internal class RegularNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RegularNDOTHours;
    protected override IOtRateStrategy RateStrategy { get; } =
        new CompoundedOtRateStrategy(RateType.OVERTIME, null);
}

internal class RestDayNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDayNDOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.RESTDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.RESTDAY_DUTY);
}
internal class LegalHolNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.LegalHolNightDiffOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.LEGAL_HOLIDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.LEGAL_HOLIDAY_DUTY);
}

internal class RestLegalDayNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestLegalDayNDOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.RESTLEGAL_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY);
}

internal class SpecialNonWorkingNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.SpecialHolNightDiffOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.SPECIAL_HOLIDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.SPECIAL_NON_WORKING);
}

internal class RestSpecialDayNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestSpecialDayNDOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.RESTSPECIAL_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.RESTDAY_SPECIAL);
}
internal class DoubleLegalNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.DoubleLegalNDOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.DOUBLELEGAL_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY);
}
internal class RestDoubleLegalNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDoubleLegalNDOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.RESTDOUBLELEGAL_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY);
}