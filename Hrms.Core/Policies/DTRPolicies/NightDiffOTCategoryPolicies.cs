namespace Hrms.Core.Policies.DTRPolicies;

/// <summary>
/// One dedicated policy per WorkType/NDOT-hours column (overtime hours that were also
/// worked at night) — replaces the OT+ND handlers that used to live inside the
/// consolidated NightDiffPolicy. Formula (nd - 1.00) is unchanged from the original
/// handlers — the OT rate was never actually applied to the ND premium there either,
/// only the ND rate; NDOT differs from plain ND only in which raw hours column it reads.
/// </summary>
internal abstract class SingleCategoryNDOTPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    protected SingleCategoryNDOTPolicy() : base(new IsEligibleForNightDifferential(), SpecFailBehaviour.ReturnInput) { }

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

internal class RegularNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RegularNDOTHours;
}

internal class RestDayNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDayNDOTHours;
}

internal class LegalHolNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.LegalHolNightDiffOTHours;
}

internal class RestLegalDayNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestLegalDayNDOTHours;
}
internal class SpecialNonWorkingNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.SpecialHolNightDiffOTHours;
}

internal class RestSpecialDayNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestSpecialDayNDOTHours;
}

internal class DoubleLegalNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.DoubleLegalNDOTHours;
}

internal class RestDoubleLegalNDOTPolicy : SingleCategoryNDOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDoubleLegalNDOTHours;
}
