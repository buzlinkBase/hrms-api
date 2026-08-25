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