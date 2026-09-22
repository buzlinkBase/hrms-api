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

        var (dayRate, _) = RateStrategy.Resolve(context);
        var otRateMultiplier = RateStrategy.ResolveRawOtRate(context);

        // Setup > Company Policy > OT/ND Calculation Method. Compounded (default, DOLE-standard):
        // day x OT, multiplicatively. Additive (opt-in, company-wide only): the day-type
        // multiplier is DROPPED entirely -- only the raw OT rate applies. Confirmed against the
        // company's own manual payroll worksheet: OT hours price at the flat OT rate alone, not
        // day-rate x OT-rate. See NightDiffOTCategoryPolicies for the OT+ND combo, and
        // OtNdCalculationMethod's own doc comment.
        var isAdditive = context.Payload.CompanyPolicy.OtNdCalculationMethod == OtNdCalculationMethod.Additive;
        var coreDayRate = isAdditive ? 0m : dayRate;
        var fullRate = isAdditive ? otRateMultiplier : dayRate * otRateMultiplier;

        // OTPremium is the pure OT variance above the base day rate -- when a client override
        // sets a flat OT rate lower than their own standard day rate, this is negative, which is
        // mathematically correct (their negotiated OT rate really is lower), just unusual to see
        // broken out as a "premium" on a report. Under Additive mode, coreDayRate is 0, so the
        // entire OT amount is "premium" -- there's no separate day-rate portion for these hours.
        line.Value += basePayForHours * fullRate;
        line.OTPremium += basePayForHours * (fullRate - coreDayRate);
        // line.NdPremium is intentionally not altered here because no ND hours exist in this pipeline category.

        // Segregated "OT Base" recording -- always the raw OT rate ALONE (ignoring day-type
        // compounding), matching the client-approved "hours x one clean rate" Hours-format
        // payslip presentation. Mode-independent: under Additive mode, the Value formula above
        // already drops the day-type multiplier for these hours entirely, so the raw OT rate
        // alone IS the whole contribution (no separate day-rate portion to net out).
        line.FlatOvertimeBase += basePayForHours * otRateMultiplier;
        return line;
    }
}

internal class RegularOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RegularOTHours;
    protected override IOtRateStrategy RateStrategy { get; } =
        new CompoundedOtRateStrategy(RateType.OVERTIME, null);
}

internal class RestDayOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDayOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.RESTDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.RESTDAY_DUTY);
}

internal class LegalHolOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.LegalHolOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.LEGAL_HOLIDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.LEGAL_HOLIDAY_DUTY);
}

internal class RestLegalDayOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestLegalDayOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.RESTLEGAL_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY);
}

internal class SpecialNonWorkingOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.SpecialHolOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.SPECIAL_HOLIDAY_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.SPECIAL_NON_WORKING);
}

internal class RestSpecialDayOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestSpecialDayOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.RESTSPECIAL_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.RESTDAY_SPECIAL);
}

internal class DoubleLegalOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.DoubleLegalOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.DOUBLELEGAL_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY);
}

internal class RestDoubleLegalOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDoubleLegalOTHours;
    protected override IOtRateStrategy RateStrategy { get; } = new CompoundedOtRateStrategy(
        RateType.RESTDOUBLELEGAL_OT_PREMIUM, RateType.HOLIDAY_OT, RateType.RESTDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY, RateType.LEGAL_HOLIDAY_DUTY);
}
