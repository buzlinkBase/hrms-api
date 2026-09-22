namespace Hrms.Core.Calculators;

public class BasicPayrollCalculator : ICalculator<DTRPayModel, PayrollContext>
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

    public DTRPayModel Calculate(PayrollContext context)
    {
        var regular = _regularPipeline.Run(context).Value;
        var restday = _restDayPipeline.Run(context).Value;
        var legal = _legalPipeline.Run(context);
        var special = _specialPipeline.Run(context).Value;
        var restLegal = _restLegalPipeline.Run(context);
        var restSpecial = _restSpecialPipeline.Run(context).Value;
        var doubleLegal = _doubleLegalPipeline.Run(context);
        var restDoubleLegal = _restDoubleLegalPipeline.Run(context);

        var regularOTResult = _regularOTPipeline.Run(context);
        var restOTResult = _restOTPipeline.Run(context);
        var legalOTResult = _legalOTPipeline.Run(context);
        var specialOTResult = _specialOTPipeline.Run(context);
        var restLegalOTResult = _restLegalOTPipeline.Run(context);
        var restSpecialOTResult = _restSpecialOTPipeline.Run(context);
        var doubleLegalOTResult = _doubleLegalOTPipeline.Run(context);
        var restDoubleLegalOTResult = _restDoubleLegalOTPipeline.Run(context);

        var regularOT = regularOTResult.Value;
        var restOT = restOTResult.Value;
        var legalOT = legalOTResult.Value;
        var specialOT = specialOTResult.Value;
        var restLegalOT = restLegalOTResult.Value;
        var restSpecialOT = restSpecialOTResult.Value;
        var doubleLegalOT = doubleLegalOTResult.Value;
        var restDoubleLegalOT = restDoubleLegalOTResult.Value;

        var otTotal =
            regularOT
            + restOT
            + legalOT
            + restLegalOT
            + specialOT
            + restSpecialOT
            + doubleLegalOT
            + restDoubleLegalOT;

        var regularNDResult = _regularNDPipeline.Run(context);
        var restNDResult = _restNDPipeline.Run(context);
        var legalNDResult = _legalNDPipeline.Run(context);
        var specialNDResult = _specialNDPipeline.Run(context);
        var restLegalNDResult = _restLegalNDPipeline.Run(context);
        var restSpecialNDResult = _restSpecialNDPipeline.Run(context);
        var doubleLegalNDResult = _doubleLegalNDPipeline.Run(context);
        var restDoubleLegalNDResult = _restDoubleLegalNDPipeline.Run(context);

        var regularND = regularNDResult.Value;
        var restND = restNDResult.Value;
        var legalND = legalNDResult.Value;
        var specialND = specialNDResult.Value;
        var restLegalND = restLegalNDResult.Value;
        var restSpecialND = restSpecialNDResult.Value;
        var doubleLegalND = doubleLegalNDResult.Value;
        var restDoubleLegalND = restDoubleLegalNDResult.Value;

        var ndTotal = regularND
            + restND
            + legalND
            + restLegalND
            + specialND
            + restSpecialND
            + doubleLegalND
            + restDoubleLegalND;

        var regularNDOTResult = _regularNDOTPipeline.Run(context);
        var restNDOTResult = _restNDOTPipeline.Run(context);
        var legalNDOTResult = _legalNDOTPipeline.Run(context);
        var restLegalNDOTResult = _restLegalNDOTPipeline.Run(context);
        var specialNDOTResult = _specialNDOTPipeline.Run(context);
        var restSpecialNDOTResult = _restSpecialNDOTPipeline.Run(context);
        var doubleLegalNDOTResult = _doubleLegalNDOTPipeline.Run(context);
        var restDoubleLegalNDOTResult = _restDoubleLegalNDOTPipeline.Run(context);

        var regularNDOT = regularNDOTResult.Value;
        var restNDOT = restNDOTResult.Value;
        var legalNDOT = legalNDOTResult.Value;
        var restLegalNDOT = restLegalNDOTResult.Value;
        var specialNDOT = specialNDOTResult.Value;
        var restSpecialNDOT = restSpecialNDOTResult.Value;
        var doubleLegalNDOT = doubleLegalNDOTResult.Value;
        var restDoubleLegalNDOT = restDoubleLegalNDOTResult.Value;

        var ndotTotal = regularNDOT
            + restNDOT
            + legalNDOT
            + restLegalNDOT
            + specialNDOT
            + restSpecialNDOT
            + doubleLegalNDOT
            + restDoubleLegalNDOT;

        // Each OT/ND/NDOT policy already isolates its own premium delta (the amount above
        // the plain day-type rate) onto BasicPipelineData.OTPremium/.NDPremium — sum those
        // directly instead of deriving premiums from unrelated totals.
        var otPremiumTotal =
            regularOTResult.OTPremium
            + restOTResult.OTPremium
            + legalOTResult.OTPremium
            + restLegalOTResult.OTPremium
            + specialOTResult.OTPremium
            + restSpecialOTResult.OTPremium
            + doubleLegalOTResult.OTPremium
            + restDoubleLegalOTResult.OTPremium
            + regularNDOTResult.OTPremium
            + restNDOTResult.OTPremium
            + legalNDOTResult.OTPremium
            + restLegalNDOTResult.OTPremium
            + specialNDOTResult.OTPremium
            + restSpecialNDOTResult.OTPremium
            + doubleLegalNDOTResult.OTPremium
            + restDoubleLegalNDOTResult.OTPremium;

        var ndPremiumTotal =
            regularNDResult.NDPremium
            + restNDResult.NDPremium
            + legalNDResult.NDPremium
            + restLegalNDResult.NDPremium
            + specialNDResult.NDPremium
            + restSpecialNDResult.NDPremium
            + doubleLegalNDResult.NDPremium
            + restDoubleLegalNDResult.NDPremium
            + regularNDOTResult.NDPremium
            + restNDOTResult.NDPremium
            + legalNDOTResult.NDPremium
            + restLegalNDOTResult.NDPremium
            + specialNDOTResult.NDPremium
            + restSpecialNDOTResult.NDPremium
            + doubleLegalNDOTResult.NDPremium
            + restDoubleLegalNDOTResult.NDPremium;

        var absentResult = _absentPipeline.Run(context);
        var lateResult = _latesPipeline.Run(context);
        var utResult = _underTimePipeline.Run(context);
        var leaveResult = _leavePipeline.Run(context);
        var paid = leaveResult.Where(x => x.PayType == PayType.WithPay).Sum(x => x.Value);
        var unpaid = leaveResult.Where(x => x.PayType == PayType.WithoutPay).Sum(x => x.Value);

        return new DTRPayModel
        {
            DtrId = context.DailyRecord.Id,
            DTRRef = context.DailyRecord.BatchCode,
            Date = context.PayrollDate,
            EmployeeId = context.DailyRecord.EmployeeId,
            DailyRate = context.Employee.DailyRate,
            WorkType = context.WorkType,

            UnpaidLeave = unpaid,
            PaidLeave = paid,
            AbsentAmount = absentResult.Value,
            LateAmount = lateResult.Value,
            UTAmount = utResult.Value,

            RegularDayPay = regular,
            RegularOTPay = regularOT,
            RegularNDPay = regularND,
            RegularNDOTPay = regularNDOT,

            RestDayPay = restday,
            RestDayOTPay = restOT,
            RestDayNDPay = restND,
            RestDayNDOTPay = restNDOT,

            LegalPay = legal.Value,
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

            RestLegalPay = restLegal.Value,
            RestLegalOTPay = restLegalOT,
            RestLegalNDPay = restLegalND,
            RestLegalNDOTPay = restLegalNDOT,

            DoubleLegalPay = doubleLegal.Value,
            DoubleLegalOTPay = doubleLegalOT,
            DoubleLegalNDPay = doubleLegalND,
            DoubleLegalNDOTPay = doubleLegalNDOT,

            RestDoubleLegalPay = restDoubleLegal.Value,
            RestDoubleLegalOTPay = restDoubleLegalOT,
            RestDoubleLegalNDPay = restDoubleLegalND,
            RestDoubleLegalNDOTPay = restDoubleLegalNDOT,

            // OTBasePay/NDOTBasePay both read BasicPipelineData.FlatOvertimeBase -- the raw OT
            // rate alone (hours * rawOTRate * hourlyRate), computed directly by the plain OT
            // policy and the NDOT policy respectively (see OvertimeCategoryPolicies.cs/
            // NightDiffOTCategoryPolicies.cs). NDBasePay reads .Value - .NDPremium -- the ND
            // policy already isolates that delta onto BasicPipelineData.NDPremium, so this
            // leaves exactly the day-type-tier-only amount (no algebra beyond what the policy
            // already computed). NDPremiumPay/NDOTPremiumPay read .FlatNightDiffPremium, computed
            // directly by the policies against the plain base pay (not compounded with any other
            // tier). Matches the client's own spreadsheet exactly -- see
            // NightDiffBasePremiumSegregationTests.
            RegularOTBasePay = regularOTResult.FlatOvertimeBase,
            RegularNDBasePay = regularNDResult.Value - regularNDResult.NDPremium,
            RegularNDPremiumPay = regularNDResult.FlatNightDiffPremium,
            RegularNDOTBasePay = regularNDOTResult.FlatOvertimeBase,
            RegularNDOTPremiumPay = regularNDOTResult.FlatNightDiffPremium,

            RestDayOTBasePay = restOTResult.FlatOvertimeBase,
            RestDayNDBasePay = restNDResult.Value - restNDResult.NDPremium,
            RestDayNDPremiumPay = restNDResult.FlatNightDiffPremium,
            RestDayNDOTBasePay = restNDOTResult.FlatOvertimeBase,
            RestDayNDOTPremiumPay = restNDOTResult.FlatNightDiffPremium,

            LegalOTBasePay = legalOTResult.FlatOvertimeBase,
            LegalNDBasePay = legalNDResult.Value - legalNDResult.NDPremium,
            LegalNDPremiumPay = legalNDResult.FlatNightDiffPremium,
            LegalNDOTBasePay = legalNDOTResult.FlatOvertimeBase,
            LegalNDOTPremiumPay = legalNDOTResult.FlatNightDiffPremium,

            SpecialOTBasePay = specialOTResult.FlatOvertimeBase,
            SpecialNDBasePay = specialNDResult.Value - specialNDResult.NDPremium,
            SpecialNDPremiumPay = specialNDResult.FlatNightDiffPremium,
            SpecialNDOTBasePay = specialNDOTResult.FlatOvertimeBase,
            SpecialNDOTPremiumPay = specialNDOTResult.FlatNightDiffPremium,

            RestLegalOTBasePay = restLegalOTResult.FlatOvertimeBase,
            RestLegalNDBasePay = restLegalNDResult.Value - restLegalNDResult.NDPremium,
            RestLegalNDPremiumPay = restLegalNDResult.FlatNightDiffPremium,
            RestLegalNDOTBasePay = restLegalNDOTResult.FlatOvertimeBase,
            RestLegalNDOTPremiumPay = restLegalNDOTResult.FlatNightDiffPremium,

            RestSpecialOTBasePay = restSpecialOTResult.FlatOvertimeBase,
            RestSpecialNDBasePay = restSpecialNDResult.Value - restSpecialNDResult.NDPremium,
            RestSpecialNDPremiumPay = restSpecialNDResult.FlatNightDiffPremium,
            RestSpecialNDOTBasePay = restSpecialNDOTResult.FlatOvertimeBase,
            RestSpecialNDOTPremiumPay = restSpecialNDOTResult.FlatNightDiffPremium,

            DoubleLegalOTBasePay = doubleLegalOTResult.FlatOvertimeBase,
            DoubleLegalNDBasePay = doubleLegalNDResult.Value - doubleLegalNDResult.NDPremium,
            DoubleLegalNDPremiumPay = doubleLegalNDResult.FlatNightDiffPremium,
            DoubleLegalNDOTBasePay = doubleLegalNDOTResult.FlatOvertimeBase,
            DoubleLegalNDOTPremiumPay = doubleLegalNDOTResult.FlatNightDiffPremium,

            RestDoubleLegalOTBasePay = restDoubleLegalOTResult.FlatOvertimeBase,
            RestDoubleLegalNDBasePay = restDoubleLegalNDResult.Value - restDoubleLegalNDResult.NDPremium,
            RestDoubleLegalNDPremiumPay = restDoubleLegalNDResult.FlatNightDiffPremium,
            RestDoubleLegalNDOTBasePay = restDoubleLegalNDOTResult.FlatOvertimeBase,
            RestDoubleLegalNDOTPremiumPay = restDoubleLegalNDOTResult.FlatNightDiffPremium,

            LegalWorked = legal.Worked,
            LegalUnWorked = legal.UnWork,
            RestLegalWorked = restLegal.Worked,
            RestLegalUnWorked = restLegal.UnWork,
            DoubleLegalUnworked = doubleLegal.UnWork,
            DoubleLegalWorked = doubleLegal.Worked,
            RestDoubleLegalUnworked = restDoubleLegal.UnWork,
            RestDoubleLegalWorked = restDoubleLegal.Worked,

            TotalOT = otTotal,
            TotalND = ndTotal,
            TotalNDOT = ndotTotal,

            NDPremiumPay = ndPremiumTotal,
            OTPremiumPay = otPremiumTotal,

            Holiday = legal.Value // + legalOT + legalND + legalNDOT
            + special //+ specialOT + specialND + specialNDOT
            + restLegal.Value //+ restLegalOT + restLegalND + restLegalNDOT
            + restSpecial //+ restSpecialOT + restSpecialND + restSpecialNDOT
            + doubleLegal.Value //+ doubleLegalOT + doubleLegalND + doubleLegalNDOT
            + restDoubleLegal.Value, // + restDoubleLegalOT  + restDoubleLegalND + restDoubleLegalNDOT,

            TotalExcludingBasic = otTotal
            + ndTotal
            + ndotTotal
            + restday
            + legal.Value
            + special
            + restLegal.Value
            + restSpecial
            + doubleLegal.Value
            + restDoubleLegal.Value

        };
    }
}
