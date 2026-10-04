namespace Hrms.Core.Calculators.DtrPay;

// Builder: assembles the per-day DTRPayModel from the 8 categories' pay and the attendance
// amounts. One mapping shared by both pay methods, so they can't drift apart in how a figure is
// reported -- only in how it was priced. Totals are summed in the same order as before.
internal interface IDtrPayModelBuilder
{
    DTRPayModel Build(PayrollContext context, IReadOnlyDictionary<PayCategoryKind, CategoryPay> pay, AttendancePay attendance);
}

internal sealed class DtrPayModelBuilder : IDtrPayModelBuilder
{
    public DTRPayModel Build(PayrollContext context, IReadOnlyDictionary<PayCategoryKind, CategoryPay> pay, AttendancePay attendance)
    {
        var regular = pay[PayCategoryKind.Regular];
        var restDay = pay[PayCategoryKind.RestDay];
        var legal = pay[PayCategoryKind.Legal];
        var special = pay[PayCategoryKind.Special];
        var restLegal = pay[PayCategoryKind.RestLegal];
        var restSpecial = pay[PayCategoryKind.RestSpecial];
        var doubleLegal = pay[PayCategoryKind.DoubleLegal];
        var restDoubleLegal = pay[PayCategoryKind.RestDoubleLegal];

        var otTotal = regular.Ot.Value + restDay.Ot.Value + legal.Ot.Value + restLegal.Ot.Value
            + special.Ot.Value + restSpecial.Ot.Value + doubleLegal.Ot.Value + restDoubleLegal.Ot.Value;
        var ndTotal = regular.Nd.Value + restDay.Nd.Value + legal.Nd.Value + restLegal.Nd.Value
            + special.Nd.Value + restSpecial.Nd.Value + doubleLegal.Nd.Value + restDoubleLegal.Nd.Value;
        var ndotTotal = regular.Ndot.Value + restDay.Ndot.Value + legal.Ndot.Value + restLegal.Ndot.Value
            + special.Ndot.Value + restSpecial.Ndot.Value + doubleLegal.Ndot.Value + restDoubleLegal.Ndot.Value;

        // Each bucket already isolates its own premium (the amount above the plain day rate).
        var otPremiumTotal =
            regular.Ot.OTPremium + restDay.Ot.OTPremium + legal.Ot.OTPremium + restLegal.Ot.OTPremium
            + special.Ot.OTPremium + restSpecial.Ot.OTPremium + doubleLegal.Ot.OTPremium + restDoubleLegal.Ot.OTPremium
            + regular.Ndot.OTPremium + restDay.Ndot.OTPremium + legal.Ndot.OTPremium + restLegal.Ndot.OTPremium
            + special.Ndot.OTPremium + restSpecial.Ndot.OTPremium + doubleLegal.Ndot.OTPremium + restDoubleLegal.Ndot.OTPremium;
        var ndPremiumTotal =
            regular.Nd.NDPremium + restDay.Nd.NDPremium + legal.Nd.NDPremium + restLegal.Nd.NDPremium
            + special.Nd.NDPremium + restSpecial.Nd.NDPremium + doubleLegal.Nd.NDPremium + restDoubleLegal.Nd.NDPremium
            + regular.Ndot.NDPremium + restDay.Ndot.NDPremium + legal.Ndot.NDPremium + restLegal.Ndot.NDPremium
            + special.Ndot.NDPremium + restSpecial.Ndot.NDPremium + doubleLegal.Ndot.NDPremium + restDoubleLegal.Ndot.NDPremium;

        return new DTRPayModel
        {
            DtrId = context.DailyRecord.Id,
            DTRRef = context.DailyRecord.BatchCode,
            Date = context.PayrollDate,
            EmployeeId = context.DailyRecord.EmployeeId,
            DailyRate = context.Employee.DailyRate,
            WorkType = context.WorkType,

            UnpaidLeave = attendance.UnpaidLeave,
            PaidLeave = attendance.PaidLeave,
            AbsentAmount = attendance.Absent,
            LateAmount = attendance.Late,
            UTAmount = attendance.Undertime,

            RegularDayPay = regular.Day.Value,
            RegularOTPay = regular.Ot.Value,
            RegularNDPay = regular.Nd.Value,
            RegularNDOTPay = regular.Ndot.Value,

            RestDayPay = restDay.Day.Value,
            RestDayOTPay = restDay.Ot.Value,
            RestDayNDPay = restDay.Nd.Value,
            RestDayNDOTPay = restDay.Ndot.Value,

            LegalPay = legal.Day.Value,
            LegalOTPay = legal.Ot.Value,
            LegalNDPay = legal.Nd.Value,
            LegalNDOTPay = legal.Ndot.Value,

            SpecialPay = special.Day.Value,
            SpecialOTPay = special.Ot.Value,
            SpecialNDPay = special.Nd.Value,
            SpecialNDOTPay = special.Ndot.Value,

            RestSpecialPay = restSpecial.Day.Value,
            RestSpecialOTPay = restSpecial.Ot.Value,
            RestSpecialNDPay = restSpecial.Nd.Value,
            RestSpecialNDOTPay = restSpecial.Ndot.Value,

            RestLegalPay = restLegal.Day.Value,
            RestLegalOTPay = restLegal.Ot.Value,
            RestLegalNDPay = restLegal.Nd.Value,
            RestLegalNDOTPay = restLegal.Ndot.Value,

            DoubleLegalPay = doubleLegal.Day.Value,
            DoubleLegalOTPay = doubleLegal.Ot.Value,
            DoubleLegalNDPay = doubleLegal.Nd.Value,
            DoubleLegalNDOTPay = doubleLegal.Ndot.Value,

            RestDoubleLegalPay = restDoubleLegal.Day.Value,
            RestDoubleLegalOTPay = restDoubleLegal.Ot.Value,
            RestDoubleLegalNDPay = restDoubleLegal.Nd.Value,
            RestDoubleLegalNDOTPay = restDoubleLegal.Ndot.Value,

            // Single-rate segregation figures -- see DTRPayModel's own doc comment and BucketPay.
            RegularOTBasePay = regular.Ot.FlatOvertimeBase,
            RegularNDBasePay = regular.Nd.BasePortion,
            RegularNDPremiumPay = regular.Nd.FlatNightDiffPremium,
            RegularNDOTBasePay = regular.Ndot.FlatOvertimeBase,
            RegularNDOTPremiumPay = regular.Ndot.FlatNightDiffPremium,

            RestDayOTBasePay = restDay.Ot.FlatOvertimeBase,
            RestDayNDBasePay = restDay.Nd.BasePortion,
            RestDayNDPremiumPay = restDay.Nd.FlatNightDiffPremium,
            RestDayNDOTBasePay = restDay.Ndot.FlatOvertimeBase,
            RestDayNDOTPremiumPay = restDay.Ndot.FlatNightDiffPremium,

            LegalOTBasePay = legal.Ot.FlatOvertimeBase,
            LegalNDBasePay = legal.Nd.BasePortion,
            LegalNDPremiumPay = legal.Nd.FlatNightDiffPremium,
            LegalNDOTBasePay = legal.Ndot.FlatOvertimeBase,
            LegalNDOTPremiumPay = legal.Ndot.FlatNightDiffPremium,

            SpecialOTBasePay = special.Ot.FlatOvertimeBase,
            SpecialNDBasePay = special.Nd.BasePortion,
            SpecialNDPremiumPay = special.Nd.FlatNightDiffPremium,
            SpecialNDOTBasePay = special.Ndot.FlatOvertimeBase,
            SpecialNDOTPremiumPay = special.Ndot.FlatNightDiffPremium,

            RestLegalOTBasePay = restLegal.Ot.FlatOvertimeBase,
            RestLegalNDBasePay = restLegal.Nd.BasePortion,
            RestLegalNDPremiumPay = restLegal.Nd.FlatNightDiffPremium,
            RestLegalNDOTBasePay = restLegal.Ndot.FlatOvertimeBase,
            RestLegalNDOTPremiumPay = restLegal.Ndot.FlatNightDiffPremium,

            RestSpecialOTBasePay = restSpecial.Ot.FlatOvertimeBase,
            RestSpecialNDBasePay = restSpecial.Nd.BasePortion,
            RestSpecialNDPremiumPay = restSpecial.Nd.FlatNightDiffPremium,
            RestSpecialNDOTBasePay = restSpecial.Ndot.FlatOvertimeBase,
            RestSpecialNDOTPremiumPay = restSpecial.Ndot.FlatNightDiffPremium,

            DoubleLegalOTBasePay = doubleLegal.Ot.FlatOvertimeBase,
            DoubleLegalNDBasePay = doubleLegal.Nd.BasePortion,
            DoubleLegalNDPremiumPay = doubleLegal.Nd.FlatNightDiffPremium,
            DoubleLegalNDOTBasePay = doubleLegal.Ndot.FlatOvertimeBase,
            DoubleLegalNDOTPremiumPay = doubleLegal.Ndot.FlatNightDiffPremium,

            RestDoubleLegalOTBasePay = restDoubleLegal.Ot.FlatOvertimeBase,
            RestDoubleLegalNDBasePay = restDoubleLegal.Nd.BasePortion,
            RestDoubleLegalNDPremiumPay = restDoubleLegal.Nd.FlatNightDiffPremium,
            RestDoubleLegalNDOTBasePay = restDoubleLegal.Ndot.FlatOvertimeBase,
            RestDoubleLegalNDOTPremiumPay = restDoubleLegal.Ndot.FlatNightDiffPremium,

            LegalWorked = legal.Day.Worked,
            LegalUnWorked = legal.Day.Unworked,
            RestLegalWorked = restLegal.Day.Worked,
            RestLegalUnWorked = restLegal.Day.Unworked,
            DoubleLegalUnworked = doubleLegal.Day.Unworked,
            DoubleLegalWorked = doubleLegal.Day.Worked,
            RestDoubleLegalUnworked = restDoubleLegal.Day.Unworked,
            RestDoubleLegalWorked = restDoubleLegal.Day.Worked,

            TotalOT = otTotal,
            TotalND = ndTotal,
            TotalNDOT = ndotTotal,

            NDPremiumPay = ndPremiumTotal,
            OTPremiumPay = otPremiumTotal,

            // Holiday categories' plain worked/unworked pay only -- their OT/ND/NDOT is in the totals above.
            Holiday = legal.Day.Value
                + special.Day.Value
                + restLegal.Day.Value
                + restSpecial.Day.Value
                + doubleLegal.Day.Value
                + restDoubleLegal.Day.Value,

            TotalExcludingBasic = otTotal
                + ndTotal
                + ndotTotal
                + restDay.Day.Value
                + legal.Day.Value
                + special.Day.Value
                + restLegal.Day.Value
                + restSpecial.Day.Value
                + doubleLegal.Day.Value
                + restDoubleLegal.Day.Value,
        };
    }
}
