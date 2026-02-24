namespace Hrms.Core.Policies.DTRPolicies
{
    public interface INightdiffHandler : ITimeHandler { }
    internal class NightDiffPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
    {
        private readonly List<ITimeHandler> _handlers = new();
        public NightDiffPolicy() : base(new IsEligibleForNightDifferential(), SpecFailBehaviour.ReturnInput)
        {
            Sethandlerspremiums();
        }
        private void Sethandlerspremiums()
        {
            // Regular Workday ND (10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RegularWorkDay,
                r => (decimal)r.RegularNDHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    return hr * baseRate * (nd - 1.00m);
                },
                "RegularWorkDay"));

            // Rest Day ND (30% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RestDayDuty,
                r => (decimal)r.RestDayNDHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    //var rest = PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "RestDayDuty"));

            // Regular Holiday ND (100% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RegularHolidayDuty,
                r => (decimal)r.LegalHolNightDiffHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    var hol = PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "RegularHolidayDuty"));

            // Rest Day + Legal Holiday ND (30% + 100% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RestDayLegalHolidayDuty,
                r => (decimal)r.LegalHolNightDiffHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    var rest = PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY);
                    var hol = PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "RestDayLegalHolidayDuty"));

            // Special Holiday ND (Working) (0% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.SpecialHolidayDuty,
                r => (decimal)r.SpecialHolNightDiffHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    var special = PremiumRateHelper.GetRate(ctx, RateType.SPECIAL_WORKING, RATE_DEFAULT.SPECIAL_WORKING);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "SpecialHolidayDuty"));

            // Special Holiday ND (Non-Working) (30% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.SpecialHolidayDutyNW,
                r => (decimal)r.SpecialHolNightDiffHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    var specialNW = PremiumRateHelper.GetRate(ctx, RateType.SPECIAL_NON_WORKING, RATE_DEFAULT.SPECIAL_NON_WORKING);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "SpecialHolidayDutyNW"));

            // Rest Day + Special Holiday ND (50% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RestDaySpecialHolidayDuty,
                r => (decimal)r.SpecialHolNightDiffHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    var restSpecial = PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_SPECIAL, RATE_DEFAULT.RESTDAY_SPECIAL);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "RestDaySpecialHolidayDuty"));

            // OVERTIME SCENARIOS WITH ND

            // Regular OT + ND (25% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RegularWorkDay,
                r => (decimal)r.RegularNDOTHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    var ot = PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "RegularWorkDayOT"));

            // Rest Day OT + ND (30% + 25% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RestDayDuty,
                r => (decimal)r.RestDayNDOTHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    var rest = PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY);
                    var ot = PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "RestDayDutyOT"));

            // Regular Holiday OT + ND (100% + 25% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RegularHolidayDuty,
                r => (decimal)r.LegalHolNightDiffOTHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    var hol = PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY);
                    var ot = PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "RegularHolidayDutyOT"));

            // Rest Day + Legal Holiday OT + ND (30% + 100% + 25% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RestDayLegalHolidayDuty,
                r => (decimal)r.LegalHolNightDiffOTHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    var rest = PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY);
                    var hol = PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY);
                    var ot = PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "RestDayLegalHolidayDutyOT"));

            // Special Holiday OT + ND (Working) (0% + 25% + 10% premium)
            // Special Holiday OT + ND (Working) (0% + 25% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.SpecialHolidayDuty,
                r => (decimal)r.SpecialHolNightDiffOTHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    var special = PremiumRateHelper.GetRate(ctx, RateType.SPECIAL_WORKING, RATE_DEFAULT.SPECIAL_WORKING);
                    var ot = PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "SpecialHolidayDutyOT"));

            // Special Holiday OT + ND (Non-Working) (30% + 25% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.SpecialHolidayDutyNW,
                r => (decimal)r.SpecialHolNightDiffOTHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    var specialNW = PremiumRateHelper.GetRate(ctx, RateType.SPECIAL_NON_WORKING, RATE_DEFAULT.SPECIAL_NON_WORKING);
                    var ot = PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "SpecialHolidayDutyNWOT"));

            // Rest Day + Special Holiday OT + ND (50% + 25% + 10% premium)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RestDaySpecialHolidayDuty,
                r => (decimal)r.SpecialHolNightDiffOTHours,
                (ctx, hr) =>
                {
                    var baseRate = PremiumRateHelper.GetHourlyRate(ctx);
                    var nd = PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                    var restSpecial = PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_SPECIAL, RATE_DEFAULT.RESTDAY_SPECIAL);
                    var ot = PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                    return hr * baseRate * ((nd - 1.00m));
                },
                "RestDaySpecialHolidayDutyOT"));
        }

        private void SethandlersIncludeBase()
        {
            // Regular Workday ND
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RegularWorkDay,
                r => (decimal)r.RegularNDHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
                },
                "RegularWorkDay"));

            // Rest Day ND
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RestDayDuty,
                r => (decimal)r.RestDayNDHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY);
                },
                "RestDayDuty"));

            // Regular Holiday ND
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RegularHolidayDuty,
                r => (decimal)r.LegalHolNightDiffHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY);
                },
                "RegularHolidayDuty"));

            // Rest Day + Legal Holiday ND
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RestDayLegalHolidayDuty,
                r => (decimal)r.LegalHolNightDiffHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY)
                             * PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY);
                },
                "RestDayLegalHolidayDuty"));

            // Special Holiday ND (Working)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.SpecialHolidayDuty,
                r => (decimal)r.SpecialHolNightDiffHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.SPECIAL_WORKING, RATE_DEFAULT.SPECIAL_WORKING);
                },
                "SpecialHolidayDuty"));

            // Special Holiday ND (Non-Working)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.SpecialHolidayDutyNW,
                r => (decimal)r.SpecialHolNightDiffHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.SPECIAL_NON_WORKING, RATE_DEFAULT.SPECIAL_NON_WORKING);
                },
                "SpecialHolidayDutyNW"));

            // Rest Day + Special Holiday ND
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RestDaySpecialHolidayDuty,
                r => (decimal)r.SpecialHolNightDiffHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_SPECIAL, RATE_DEFAULT.RESTDAY_SPECIAL);
                },
                "RestDaySpecialHolidayDuty"));

            // OVERTIME SCENARIOS WITH ND

            // Regular OT + ND
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RegularWorkDay,
                r => (decimal)r.RegularNDOTHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                },
                "RegularWorkDayOT"));

            // Rest Day OT + ND
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RestDayDuty,
                r => (decimal)r.RestDayNDOTHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY)
                             * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                },
                "RestDayDutyOT"));

            // Regular Holiday OT + ND
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RegularHolidayDuty,
                r => (decimal)r.LegalHolNightDiffOTHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY)
                             * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                },
                "RegularHolidayDutyOT"));

            // Rest Day + Legal Holiday OT + ND
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RestDayLegalHolidayDuty,
                r => (decimal)r.LegalHolNightDiffOTHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_DUTY, RATE_DEFAULT.RESTDAY_DUTY)
                             * PremiumRateHelper.GetRate(ctx, RateType.LEGAL_HOLIDAY_DUTY, RATE_DEFAULT.LEGAL_HOLIDAY_DUTY)
                             * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                },
                "RestDayLegalHolidayDutyOT"));

            // Special Holiday OT + ND (Working)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.SpecialHolidayDuty,
                r => (decimal)r.SpecialHolNightDiffOTHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.SPECIAL_WORKING, RATE_DEFAULT.SPECIAL_WORKING)
                             * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                },
                "SpecialHolidayDutyOT"));

            // Special Holiday OT + ND (Non-Working)
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.SpecialHolidayDutyNW,
                r => (decimal)r.SpecialHolNightDiffOTHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.SPECIAL_NON_WORKING, RATE_DEFAULT.SPECIAL_NON_WORKING)
                             * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                },
                "SpecialHolidayDutyNWOT"));

            // Rest Day + Special Holiday OT + ND
            _handlers.Add(new NightDiffTimeHandler(
                WorkType.RestDaySpecialHolidayDuty,
                r => (decimal)r.SpecialHolNightDiffOTHours,
                (ctx, hr) =>
                {
                    return hr * PremiumRateHelper.GetHourlyRate(ctx)
                             * PremiumRateHelper.GetRate(ctx, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF)
                             * PremiumRateHelper.GetRate(ctx, RateType.RESTDAY_SPECIAL, RATE_DEFAULT.RESTDAY_SPECIAL)
                             * PremiumRateHelper.GetRate(ctx, RateType.OVERTIME, RATE_DEFAULT.OVERTIME);
                },
                "RestDaySpecialHolidayDutyOT"));
        }
        public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
        {
            var results = _handlers
                 .Where(x => x.CanHandle(context))
                .Select(h => h.Process(context))
                .ToList();

            //var ndRate= PremiumRateHelper.GetRate(context,  RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF);
            var Amt = results.Sum(r => r.Amount) * 1;
            var totalHr = results.Sum(r => r.Hr);

            line.NightDiffInfo = new NightDiffInfo
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
}

public class NightDiffTimeHandler : ITimeHandler
{
    private readonly WorkType _workType;
    private readonly Func<DailyRecordRunModel, decimal> _hoursSelector;
    private readonly Func<PayrollContext, decimal, decimal> _rateSelector;
    private readonly string _handlerName;

    public NightDiffTimeHandler(
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
        var amount = _rateSelector.Invoke(context, hr);
        return new PremiumRecordModel(_handlerName, hr, amount);
    }
}