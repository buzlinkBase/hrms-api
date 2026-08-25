namespace Hrms.Core.Calculators;

public class BasicPayrollCalculator : ICalculator<BasicRateModel, PayrollContext>
{
    private readonly RegularPipeline _regularPipeline;
    private readonly RestDayPipeLine _restDayPipeline;
    private readonly RegularHolidayPipeLine _legalPipeline;
    private readonly SpecialWorkDayPipeLine _specialPipeline;
    private readonly RestLegalDayPipeLine _restLegalPipeline;
    private readonly RestSpecialDayPipeLine _restSpecialPipeline;
    private readonly DoubleLegalPipeLine _doubleLegalPipeline;
    private readonly RestDoubleLegalPipeLine _restDoubleLegalPipeline;

    private readonly RegularOTPipeLine _regularOTPipeline;
    private readonly RestDayOTPipeLine _restOTPipeline;
    private readonly LegalHolOTPipeLine _legalOTPipeline;
    private readonly SpecialNonWorkingOTPipeLine _specialOTPipeline;
    private readonly RestLegalDayOTPipeLine _restLegalOTPipeline;
    private readonly RestSpecialDayOTPipeLine _restSpecialOTPipeline;
    private readonly DoubleLegalOTPipeLine _doubleLegalOTPipeline;
    private readonly RestDoubleLegalOTPipeLine _restDoubleLegalOTPipeline;

    private readonly RegularNDPipeLine _regularNDPipeline;
    private readonly RestDayNDPipeLine _restNDPipeline;
    private readonly LegalHolNDPipeLine _legalNDPipeline;
    private readonly SpecialNonWorkingNDPipeLine _specialNDPipeline;
    private readonly RestLegalDayNDPipeLine _restLegalNDPipeline;
    private readonly RestSpecialDayNDPipeLine _restSpecialNDPipeline;
    private readonly DoubleLegalNDPipeLine _doubleLegalNDPipeline;
    private readonly RestDoubleLegalNDPipeLine _restDoubleLegalNDPipeline;

    private readonly RegularNDOTPipeLine _regularNDOTPipeline;
    private readonly RestDayNDOTPipeLine _restNDOTPipeline;
    private readonly LegalHolNDOTPipeLine _legalNDOTPipeline;
    private readonly RestLegalDayNDOTPipeLine _restLegalNDOTPipeline;
    private readonly SpecialNonWorkingNDOTPipeLine _specialNDOTPipeline;
    private readonly RestSpecialDayNDOTPipeLine _restSpecialNDOTPipeline;
    private readonly DoubleLegalNDOTPipeLine _doubleLegalNDOTPipeline;
    private readonly RestDoubleLegalNDOTPipeLine _restDoubleLegalNDOTPipeline;

    private readonly AbsentPipeline _absentPipeline;
    private readonly LatesPipeLine _latesPipeline;
    private readonly UnderTimePipeLine _underTimePipeline;
    private readonly LeavePipeline _leavePipeline;

    public BasicPayrollCalculator(
        RegularPipeline regularPipeline,
        RestDayPipeLine restDayPipeline,
        RegularHolidayPipeLine legalPipeline,
        SpecialWorkDayPipeLine specialPipeline,
        RestLegalDayPipeLine restLegalPipeline,
        RestSpecialDayPipeLine restSpecialPipeline,
        DoubleLegalPipeLine doubleLegalPipeline,
        RestDoubleLegalPipeLine restDoubleLegalPipeline,
        RegularOTPipeLine regularOTPipeline,
        RestDayOTPipeLine restOTPipeline,
        LegalHolOTPipeLine legalOTPipeline,
        SpecialNonWorkingOTPipeLine specialOTPipeline,
        RestLegalDayOTPipeLine restLegalOTPipeline,
        RestSpecialDayOTPipeLine restSpecialOTPipeline,
        DoubleLegalOTPipeLine doubleLegalOTPipeline,
        RestDoubleLegalOTPipeLine restDoubleLegalOTPipeline,
        RegularNDPipeLine regularNDPipeline,
        RestDayNDPipeLine restNDPipeline,
        LegalHolNDPipeLine legalNDPipeline,
        SpecialNonWorkingNDPipeLine specialNDPipeline,
        RestLegalDayNDPipeLine restLegalNDPipeline,
        RestSpecialDayNDPipeLine restSpecialNDPipeline,
        DoubleLegalNDPipeLine doubleLegalNDPipeline,
        RestDoubleLegalNDPipeLine restDoubleLegalNDPipeline,
        RegularNDOTPipeLine regularNDOTPipeline,
        RestDayNDOTPipeLine restNDOTPipeline,
        LegalHolNDOTPipeLine legalNDOTPipeline,
        RestLegalDayNDOTPipeLine restLegalNDOTPipeline,
        SpecialNonWorkingNDOTPipeLine specialNDOTPipeline,
        RestSpecialDayNDOTPipeLine restSpecialNDOTPipeline,
        DoubleLegalNDOTPipeLine doubleLegalNDOTPipeline,
        RestDoubleLegalNDOTPipeLine restDoubleLegalNDOTPipeline,
        AbsentPipeline absentPipeline,
        LatesPipeLine latesPipeline,
        UnderTimePipeLine underTimePipeline,
        LeavePipeline leavePipeline)
    {
        _regularPipeline = regularPipeline;
        _restDayPipeline = restDayPipeline;
        _legalPipeline = legalPipeline;
        _specialPipeline = specialPipeline;
        _restLegalPipeline = restLegalPipeline;
        _restSpecialPipeline = restSpecialPipeline;
        _doubleLegalPipeline = doubleLegalPipeline;
        _restDoubleLegalPipeline = restDoubleLegalPipeline;

        _regularOTPipeline = regularOTPipeline;
        _restOTPipeline = restOTPipeline;
        _legalOTPipeline = legalOTPipeline;
        _specialOTPipeline = specialOTPipeline;
        _restLegalOTPipeline = restLegalOTPipeline;
        _restSpecialOTPipeline = restSpecialOTPipeline;
        _doubleLegalOTPipeline = doubleLegalOTPipeline;
        _restDoubleLegalOTPipeline = restDoubleLegalOTPipeline;

        _regularNDPipeline = regularNDPipeline;
        _restNDPipeline = restNDPipeline;
        _legalNDPipeline = legalNDPipeline;
        _specialNDPipeline = specialNDPipeline;
        _restLegalNDPipeline = restLegalNDPipeline;
        _restSpecialNDPipeline = restSpecialNDPipeline;
        _doubleLegalNDPipeline = doubleLegalNDPipeline;
        _restDoubleLegalNDPipeline = restDoubleLegalNDPipeline;

        _regularNDOTPipeline = regularNDOTPipeline;
        _restNDOTPipeline = restNDOTPipeline;
        _legalNDOTPipeline = legalNDOTPipeline;
        _restLegalNDOTPipeline = restLegalNDOTPipeline;
        _specialNDOTPipeline = specialNDOTPipeline;
        _restSpecialNDOTPipeline = restSpecialNDOTPipeline;
        _doubleLegalNDOTPipeline = doubleLegalNDOTPipeline;
        _restDoubleLegalNDOTPipeline = restDoubleLegalNDOTPipeline;

        _absentPipeline = absentPipeline;
        _latesPipeline = latesPipeline;
        _underTimePipeline = underTimePipeline;
        _leavePipeline = leavePipeline;
    }

    public BasicRateModel Calculate(PayrollContext context)
    {
        var regular = _regularPipeline.Run(context).Value;
        var restday = _restDayPipeline.Run(context).Value;
        var legal = _legalPipeline.Run(context).Value;
        var special = _specialPipeline.Run(context).Value;
        var restlegal = _restLegalPipeline.Run(context).Value;
        var restSpecial = _restSpecialPipeline.Run(context).Value;
        var doubleLegal = _doubleLegalPipeline.Run(context).Value;
        var restDoubleLegal = _restDoubleLegalPipeline.Run(context).Value;

        var regularOT = _regularOTPipeline.Run(context).Value;
        var restOT = _restOTPipeline.Run(context).Value;
        var legalOT = _legalOTPipeline.Run(context).Value;
        var specialOT = _specialOTPipeline.Run(context).Value;
        var restLegalOT = _restLegalOTPipeline.Run(context).Value;
        var restSpecialOT = _restSpecialOTPipeline.Run(context).Value;
        var doubleLegalOT = _doubleLegalOTPipeline.Run(context).Value;
        var restDoubleLegalOT = _restDoubleLegalOTPipeline.Run(context).Value;

        var otTotal = regularOT
            + restOT
            + legalOT
            + restLegalOT
            + specialOT
            + restSpecialOT
            + doubleLegalOT
            + restDoubleLegalOT;
    
        var regularND = _regularNDPipeline.Run(context).Value;
        var restND = _restNDPipeline.Run(context).Value;
        var legalND = _legalNDPipeline.Run(context).Value;
        var specialND = _specialNDPipeline.Run(context).Value;
        var restLegalND = _restLegalNDPipeline.Run(context).Value;
        var restSpecialND = _restSpecialNDPipeline.Run(context).Value;
        var doubleLegalND = _doubleLegalNDPipeline.Run(context).Value;
        var restDoubleLegalND = _restDoubleLegalNDPipeline.Run(context).Value;

        var ndTotal = regularND
            + restND
            + legalND
            + restLegalND
            + specialND
            + restSpecialND
            + doubleLegalND
            + restDoubleLegalND;

        var regularNDOT = _regularNDOTPipeline.Run(context).Value;
        var restNDOT = _restNDOTPipeline.Run(context).Value;
        var legalNDOT = _legalNDOTPipeline.Run(context).Value;
        var restLegalNDOT = _restLegalNDOTPipeline.Run(context).Value;
        var specialNDOT = _specialNDOTPipeline.Run(context).Value;
        var restSpecialNDOT = _restSpecialNDOTPipeline.Run(context).Value;
        var doubleLegalNDOT = _doubleLegalNDOTPipeline.Run(context).Value;
        var restDoubleLegalNDOT = _restDoubleLegalNDOTPipeline.Run(context).Value;

        var ndotTotal = regularNDOT
            + restNDOT
            + legalNDOT
            + restLegalNDOT
            + specialNDOT
            + restSpecialNDOT
            + doubleLegalNDOT
            + restDoubleLegalNDOT;

        var ndAndNdotHours = (decimal)(context.DailyRecord.RegularNDHours + context.DailyRecord.RegularNDOTHours
            + context.DailyRecord.RestDayNDHours + context.DailyRecord.RestDayNDOTHours
            + context.DailyRecord.LegalHolNightDiffHours + context.DailyRecord.LegalHolNightDiffOTHours
            + context.DailyRecord.RestLegalDayNDHours + context.DailyRecord.RestLegalDayNDOTHours
            + context.DailyRecord.SpecialHolNightDiffHours + context.DailyRecord.SpecialHolNightDiffOTHours
            + context.DailyRecord.RestSpecialDayNDHours + context.DailyRecord.RestSpecialDayNDOTHours
            + context.DailyRecord.DoubleLegalNDHours + context.DailyRecord.DoubleLegalNDOTHours
            + context.DailyRecord.RestDoubleLegalNDHours + context.DailyRecord.RestDoubleLegalNDOTHours);

        var absentResult = _absentPipeline.Run(context);
        var lateResult = _latesPipeline.Run(context);
        var utResult = _underTimePipeline.Run(context);
        var leaveResult = _leavePipeline.Run(context);

        var paid = leaveResult.Where(x => x.PayType == PayType.WithPay).Sum(x => x.Value);
        var unpaid = leaveResult.Where(x => x.PayType == PayType.WithoutPay).Sum(x => x.Value);

        return new BasicRateModel
        {
            DtrId = context.DailyRecord.Id,
            DTRRef = context.DailyRecord.BatchCode,
            Date = context.PayrollDate,
            EmployeeId = context.Employee.Id,

            UnpaidLeave = unpaid,
            PaidLeave = paid,
            AbsentAmount = absentResult.Value,
            LateAmount = lateResult.Value,
            UTAmount = utResult.Value,
            BasicPay = regular,

            RegularDayPay = regular,
            RegularOTPay = regularOT,
            RegularNDPay = regularND,
            RegularNDOTPay = regularNDOT,

            RestDayPay = restday,
            RestDayOTPay = restOT,
            RestDayNDPay = restND,
            RestDayNDOTPay = restNDOT,

            LegalPay = legal,
            LegalOTPay = legalOT,
            LegalNDPay = legalND,
            LegalNDOTPay = legalNDOT,

            SpecialPay = special,
            SpecialOTPay = specialOT,
            SpecialNDPay = specialND,
            SpecialNDOTPay = specialNDOT,

            RestSpecialPay = restSpecial,
            RestSpecialOTPay = restSpecialOT,
            RestSpecialNDPay = restSpecialND,
            RestSpecialNDOTPay = restSpecialNDOT,

            RestLegalPay = restlegal,
            RestLegalOTPay = restLegalOT,
            RestLegalNDPay = restLegalND,
            RestLegalNDOTPay = restLegalNDOT,

            DoubleLegalPay = doubleLegal,
            DoubleLegalOTPay = doubleLegalOT,
            DoubleLegalNDPay = doubleLegalND,
            DoubleLegalNDOTPay = doubleLegalNDOT,

            RestDoubleLegalPay = restDoubleLegal,
            RestDoubleLegalOTPay = restDoubleLegalOT,
            RestDoubleLegalNDPay = restDoubleLegalND,
            RestDoubleLegalNDOTPay = restDoubleLegalNDOT,

            TotalOT=otTotal,
            TotalND=ndTotal,    
            TotalNDOT=ndTotal,
            Gross = regular
                    + restday
                    + otTotal
                    + ndTotal
                    + ndotTotal
                    + legal
                    + special
                    + restlegal
                    + restSpecial
                    + doubleLegal
                    + restDoubleLegal
        };
    }
}
