namespace Hrms.Core.Policies.DTRPolicies;

/// <summary>
/// One dedicated policy per WorkType/OT-hours column (replaces the old consolidated
/// OvertimePolicy handler list) so each category's OT pay is individually traceable on
/// BasicRateModel/Payroll instead of folded into a single aggregate OTHourInfo.Amount.
/// Formulas are unchanged from the original OvertimePolicy handlers.
/// </summary>
internal abstract class SingleCategoryOTPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    protected SingleCategoryOTPolicy() : base(new IsEligibleForOvertime(), SpecFailBehaviour.ReturnInput) { }

    protected abstract double Hours(DailyRecordRunModel r);
    protected abstract decimal ResolveMultiplier(PayrollContext context);

    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        var hours = (decimal)Hours(dailyRecord);
        if (hours <= 0) return line;
        var hourlyRate = PremiumRateHelper.GetHourlyRate(context);
        var multiplier = ResolveMultiplier(context) * PremiumRateHelper.GetRate(context, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
        line.Value += hourlyRate * hours * multiplier;
        return line;
    }
}

internal class RegularOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RegularOTHours;
    protected override decimal ResolveMultiplier(PayrollContext ctx) =>
        PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
}

internal class RestDayOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDayOTHours;
    protected override decimal ResolveMultiplier(PayrollContext ctx) =>
        PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY) *
        PremiumRateHelper.GetRate(ctx, RateType.HOLIDAY_OT, RATE_DEFAULT.HOLIDAY_OT);
}

internal class LegalHolOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.LegalHolOTHours;
    protected override decimal ResolveMultiplier(PayrollContext ctx) =>
        PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY, RATE_DEFAULT.LEGAL_HOLIDAY) *
        PremiumRateHelper.GetRate(ctx, RateType.HOLIDAY_OT, RATE_DEFAULT.HOLIDAY_OT);
}

internal class RestLegalDayOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestLegalDayOTHours;
    protected override decimal ResolveMultiplier(PayrollContext ctx) =>
        PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY)
        * PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY)
        * PremiumRateHelper.GetRate(ctx, RateType.HOLIDAY_OT, RATE_DEFAULT.HOLIDAY_OT);
}


internal class SpecialNonWorkingOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.SpecialHolOTHours;
    protected override decimal ResolveMultiplier(PayrollContext ctx) =>
         PremiumRateHelper.GetRate(ctx, RateType.SPECIAL_NON_WORKING, RATE_DEFAULT.SPECIAL_NON_WORKING)
        * PremiumRateHelper.GetRate(ctx, RateType.HOLIDAY_OT, RATE_DEFAULT.HOLIDAY_OT);
}

internal class RestSpecialDayOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestSpecialDayOTHours;
    protected override decimal ResolveMultiplier(PayrollContext ctx) =>
         PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_SPECIAL, RATE_DEFAULT.RESTDAY_SPECIAL)
        * PremiumRateHelper.GetRate(ctx, RateType.HOLIDAY_OT, RATE_DEFAULT.HOLIDAY_OT)
        ;
} 

internal class DoubleLegalOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.DoubleLegalOTHours;
    protected override decimal ResolveMultiplier(PayrollContext ctx) =>
         PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY, RATE_DEFAULT.LEGAL_HOLIDAY)
        * PremiumRateHelper.GetRate(ctx, RateType.HOLIDAY_OT, RATE_DEFAULT.HOLIDAY_OT)
        * Math.Max(2m, ctx.DailyRecord.HolCount)
        ;
}

internal class RestDoubleLegalOTPolicy : SingleCategoryOTPolicy
{
    protected override double Hours(DailyRecordRunModel r) => r.RestDoubleLegalOTHours;
    protected override decimal ResolveMultiplier(PayrollContext ctx) =>
         PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY, RATE_DEFAULT.LEGAL_HOLIDAY)
        * PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY)
        * PremiumRateHelper.GetRate(ctx, RateType.HOLIDAY_OT, RATE_DEFAULT.HOLIDAY_OT)
        * Math.Max(2m, ctx.DailyRecord.HolCount)
        ;
}
