namespace Hrms.Core.Policies.DTRPolicies;

public interface ITimeHandler
{
    bool CanHandle(PayrollContext context);
    PremiumRecordModel Process(PayrollContext context);
}
public record PremiumRecordModel(string Handler, decimal Hr, decimal Amount);
internal class OvertimePolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    private readonly List<ITimeHandler> _handlers = new();
    public OvertimePolicy() : base(new IsEligibleForOvertime(), SpecFailBehaviour.ReturnInput)
    {
        Sethandlers();
    }
    private void Sethandlers()
    {
        // Regular Workday OT
        _handlers.Add(new OvertimeHandler(
            WorkType.RegularWorkDay,
            r => (decimal)r.RegularOTHours,
            (ctx, hr) =>
            {
                return hr * PremiumRateHelper.GetHourlyRate(ctx)
                         * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
            },
            "RegularWorkDayOT"));

        // Rest Day OT
        _handlers.Add(new OvertimeHandler(
            WorkType.RestDayDuty,
            r => (decimal)r.RestDayOTHours,
            (ctx, hr) =>
            {
                return hr * PremiumRateHelper.GetHourlyRate(ctx)
                         * PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY)
                         * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
            },
            "RestDayOT"));

        // Regular Holiday OT
        _handlers.Add(new OvertimeHandler(
            WorkType.RegularHolidayDuty,
            r => (decimal)r.LegalHolOTHours,
            (ctx, hr) =>
            {
                return hr * PremiumRateHelper.GetHourlyRate(ctx)
                         * PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY)
                         * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
            },
            "LegalHolOT"));

        // Rest Day + Legal Holiday OT
        _handlers.Add(new OvertimeHandler(
            WorkType.RestDayLegalHolidayDuty,
            r => (decimal)r.LegalHolOTHours,
            (ctx, hr) =>
            {
                return hr * PremiumRateHelper.GetHourlyRate(ctx)
                         * PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY)
                         * PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY)
                         * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
            },
            "LegalRestOT"));

        // Special Holiday OT (Working)
        _handlers.Add(new OvertimeHandler(
            WorkType.SpecialHolidayDuty,
            r => (decimal)r.SpecialHolOTHours,
            (ctx, hr) =>
            {
                return hr * PremiumRateHelper.GetHourlyRate(ctx)
                         * PremiumRateHelper.GetRate(ctx, RateType.SPECIAL_WORKING, RATE_DEFAULT.SPECIAL_WORKING)
                         * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
            },
            "SpecialOTWorking"));

        // Special Holiday OT (Non-Working)
        _handlers.Add(new OvertimeHandler(
            WorkType.SpecialHolidayDutyNW,
            r => (decimal)r.SpecialHolOTHours,
            (ctx, hr) =>
            {
                return hr * PremiumRateHelper.GetHourlyRate(ctx)
                         * PremiumRateHelper.GetRate(ctx, RateType.SPECIAL_NON_WORKING, RATE_DEFAULT.SPECIAL_NON_WORKING)
                         * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
            },
            "SpecialOTNonworking"));

        // Rest Day + Special Holiday OT
        _handlers.Add(new OvertimeHandler(
            WorkType.RestDaySpecialHolidayDuty,
            r => (decimal)r.SpecialHolOTHours,
            (ctx, hr) =>
            {
                return hr * PremiumRateHelper.GetHourlyRate(ctx)
                         * PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_SPECIAL, RATE_DEFAULT.RESTDAY_SPECIAL)
                         * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
            },
            "SpecialRestOT"));
    }
    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        var results = _handlers
            .Where(x => x.CanHandle(context))
            .Select(h => h.Process(context))
            .ToList();

        var Amt = results.Sum(r => r.Amount);
        var totalHr = results.Sum(r => r.Hr);

        line.OTInfo = new OvertimeInfo
        {
            PayrollDate = context.PayrollDate,
            Hour = totalHr,
            Amount = Amt,
            Handler = string.Join(',', results.Select(r => r.Handler))
        };

        line.Value += Amt;
        return line;
    }
}

public class OvertimeHandler : ITimeHandler
{
    private readonly WorkType _workType;
    private readonly Func<DailyRecordRunModel, decimal> _hoursSelector;
    private readonly Func<PayrollContext, decimal, decimal> _rateSelector;
    private readonly string _handlerName;

    public OvertimeHandler(
        WorkType workType,
        Func<DailyRecordRunModel, decimal> hoursSelector,
        Func<PayrollContext, decimal, decimal> rateSelector,
        string handlerName)
    {
        _workType = workType;
        _hoursSelector = hoursSelector;
        _rateSelector = rateSelector;
        _handlerName = handlerName;
    }

    public bool CanHandle(PayrollContext context) => context.WorkType == _workType;

    public PremiumRecordModel Process(PayrollContext context)
    {
        var hr = _hoursSelector(context.DailyRecord);
        var amount = Math.Max(0, _rateSelector.Invoke(context, hr));
        return new PremiumRecordModel(_handlerName, hr, amount);
    }
}