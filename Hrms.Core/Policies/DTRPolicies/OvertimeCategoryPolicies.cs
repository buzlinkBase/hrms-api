namespace Hrms.Core.Policies.DTRPolicies;

internal abstract class SingleCategoryOTPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    protected SingleCategoryOTPolicy() : base(new IsEligibleForOvertime(), SpecFailBehaviour.ReturnInput) { }

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

        var isPreFunded = context.Employee.SalaryType == SalaryType.FIXED;

        var effectiveMultiplier = isPreFunded
            ? Math.Max(0m, combinedRateMultiplier - 1.00m)
            : combinedRateMultiplier;

        line.Value += hours * baseHourlyRate * effectiveMultiplier;
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