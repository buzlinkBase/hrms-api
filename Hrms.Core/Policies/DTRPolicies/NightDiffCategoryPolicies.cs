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

        decimal combinedRateMultiplier = 1.0m;
        foreach (var rateType in PolicyRateTypes)
        {
            var rate = PremiumRateHelper.GetRate(context, rateType, 1.0m);
            combinedRateMultiplier *= rate;
        }

        var isPreFunded = context.Employee.IsNightDiffIncluded ||
                          context.Employee.SalaryType == SalaryType.FIXED;

        var effectiveMultiplier = isPreFunded
            ? Math.Max(0m, combinedRateMultiplier - 1.00m)
            : combinedRateMultiplier;

        line.Value += hours * baseHourlyRate * effectiveMultiplier;
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