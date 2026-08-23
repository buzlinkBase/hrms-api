namespace Hrms.Core.Policies.DTRPolicies;

/// <summary>
/// One dedicated policy per WorkType/ND-hours column (replaces the base-ND handlers that
/// used to live inside the consolidated NightDiffPolicy) so each category's night
/// differential pay is individually traceable. Formula (nd - 1.00) is unchanged from the
/// original handlers — the night-diff premium never compounds with other day-type
/// multipliers in this codebase.
/// </summary>
internal abstract class SingleCategoryNDPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    protected SingleCategoryNDPolicy() : base(new IsEligibleForNightDifferential(), SpecFailBehaviour.ReturnInput) { }

    protected abstract double Hours(DailyRecordRunModel r);

    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        var hours = (decimal)Hours(dailyRecord);
        if (hours <= 0) return line;

        var hourlyRate = PremiumRateHelper.GetHourlyRate(context);
        var nd = PremiumRateHelper.GetRate(context, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);

        line.Value += hours * hourlyRate * (nd - 1.00m);
        return line;
    }
}

internal class RegularNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RegularNDHours;
}

internal class RestDayNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDayNDHours;
}

internal class LegalHolNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.LegalHolNightDiffHours;
}

internal class RestLegalDayNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestLegalDayNDHours;
}


internal class SpecialNonWorkingNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.SpecialHolNightDiffHours;
}

internal class RestSpecialDayNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestSpecialDayNDHours;
}


internal class DoubleLegalNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.DoubleLegalNDHours;
}

internal class RestDoubleLegalNDPolicy : SingleCategoryNDPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDoubleLegalNDHours;
}
