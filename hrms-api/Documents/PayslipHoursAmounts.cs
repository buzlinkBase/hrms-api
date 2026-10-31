using Hrms.Domain.Entities;

namespace Hrms.Api.Documents;

// One day-type category's saved hours and pay on a Payroll row, so the Hours payslip reads the 8
// categories the same way. Hours are natural: OT and ND both include the NDOT hours.
internal sealed record PayCategoryFigures(
    decimal OtHours,
    decimal NdHours,
    decimal NdotHours,
    decimal OtPay,
    decimal NdPay,
    decimal NdotPay,
    decimal OtBasePay,
    decimal NdBasePay,
    decimal NdPremiumPay,
    decimal NdotBasePay,
    decimal NdotPremiumPay)
{
    public static PayCategoryFigures Regular(Payroll p) => new(p.RegularOTHours, p.RegularNDHours, p.RegularNDOTHours,
        p.RegularOTPay, p.RegularNDPay, p.RegularNDOTPay,
        p.RegularOTBasePay, p.RegularNDBasePay, p.RegularNDPremiumPay, p.RegularNDOTBasePay, p.RegularNDOTPremiumPay);

    public static PayCategoryFigures RestDay(Payroll p) => new(p.RestDayOTHours, p.RestDayNDHours, p.RestDayNDOTHours,
        p.RestDayOTPay, p.RestDayNDPay, p.RestDayNDOTPay,
        p.RestDayOTBasePay, p.RestDayNDBasePay, p.RestDayNDPremiumPay, p.RestDayNDOTBasePay, p.RestDayNDOTPremiumPay);

    public static PayCategoryFigures Legal(Payroll p) => new(p.LegalHolOTHours, p.LegalHolNightDiffHours, p.LegalHolNightDiffOTHours,
        p.LegalOTPay, p.LegalNDPay, p.LegalNDOTPay,
        p.LegalOTBasePay, p.LegalNDBasePay, p.LegalNDPremiumPay, p.LegalNDOTBasePay, p.LegalNDOTPremiumPay);

    public static PayCategoryFigures Special(Payroll p) => new(p.SpecialHolOTHours, p.SpecialHolNightDiffHours, p.SpecialHolNightDiffOTHours,
        p.SpecialOTPay, p.SpecialNDPay, p.SpecialNDOTPay,
        p.SpecialOTBasePay, p.SpecialNDBasePay, p.SpecialNDPremiumPay, p.SpecialNDOTBasePay, p.SpecialNDOTPremiumPay);

    public static PayCategoryFigures RestLegal(Payroll p) => new(p.RestLegalDayOTHours, p.RestLegalDayNDHours, p.RestLegalDayNDOTHours,
        p.RestLegalOTPay, p.RestLegalNDPay, p.RestLegalNDOTPay,
        p.RestLegalOTBasePay, p.RestLegalNDBasePay, p.RestLegalNDPremiumPay, p.RestLegalNDOTBasePay, p.RestLegalNDOTPremiumPay);

    public static PayCategoryFigures RestSpecial(Payroll p) => new(p.RestSpecialDayOTHours, p.RestSpecialDayNDHours, p.RestSpecialDayNDOTHours,
        p.RestSpecialOTPay, p.RestSpecialNDPay, p.RestSpecialNDOTPay,
        p.RestSpecialOTBasePay, p.RestSpecialNDBasePay, p.RestSpecialNDPremiumPay, p.RestSpecialNDOTBasePay, p.RestSpecialNDOTPremiumPay);

    public static PayCategoryFigures DoubleLegal(Payroll p) => new(p.DoubleLegalOTHours, p.DoubleLegalNDHours, p.DoubleLegalNDOTHours,
        p.DoubleLegalOTPay, p.DoubleLegalNDPay, p.DoubleLegalNDOTPay,
        p.DoubleLegalOTBasePay, p.DoubleLegalNDBasePay, p.DoubleLegalNDPremiumPay, p.DoubleLegalNDOTBasePay, p.DoubleLegalNDOTPremiumPay);

    public static PayCategoryFigures RestDoubleLegal(Payroll p) => new(p.RestDoubleLegalOTHours, p.RestDoubleLegalNDHours, p.RestDoubleLegalNDOTHours,
        p.RestDoubleLegalOTPay, p.RestDoubleLegalNDPay, p.RestDoubleLegalNDOTPay,
        p.RestDoubleLegalOTBasePay, p.RestDoubleLegalNDBasePay, p.RestDoubleLegalNDPremiumPay, p.RestDoubleLegalNDOTBasePay, p.RestDoubleLegalNDOTPremiumPay);

    public static IReadOnlyList<PayCategoryFigures> All(Payroll p) =>
        [Regular(p), RestDay(p), Legal(p), Special(p), RestLegal(p), RestSpecial(p), DoubleLegal(p), RestDoubleLegal(p)];
}

// Strategy: what the Hours payslip's "{Category} OT" and merged "Night Diff." rows show, by the
// OT/ND method the run was priced with (Payroll.OtNdCalculationMethod, so an old payslip reprints
// as it was paid). Either way the rows add up to each category's Pay + OTPay + NDPay + NDOTPay, so
// the itemized Earnings reconcile to Gross Income. The day-type base row ({Category}Pay plus the
// night hours' day-rate portion, {Category}NDBasePay) is the same for both.
internal abstract class PayslipHoursAmounts
{
    private static readonly PayslipHoursAmounts AdditiveAmounts = new Additive();
    private static readonly PayslipHoursAmounts CompoundedAmounts = new Compounded();

    public static PayslipHoursAmounts For(OtNdCalculationMethod method) =>
        method == OtNdCalculationMethod.Additive ? AdditiveAmounts : CompoundedAmounts;

    public abstract decimal Ot(PayCategoryFigures c);
    public abstract decimal NightDiffHours(PayCategoryFigures c);
    public abstract decimal NightDiff(PayCategoryFigures c);

    // Each bucket is priced at one rate, so the flat figures are exactly what was paid: OT and NDOT
    // at the raw OT rate, and every night hour (NDOT included) at the night premium alone.
    private sealed class Additive : PayslipHoursAmounts
    {
        public override decimal Ot(PayCategoryFigures c) => c.OtBasePay + c.NdotBasePay;
        public override decimal NightDiffHours(PayCategoryFigures c) => c.NdHours;
        public override decimal NightDiff(PayCategoryFigures c) => c.NdPremiumPay + c.NdotPremiumPay;
    }

    // DOLE-standard: OT is compounded with the day rate, and night OT with the night rate as well,
    // so the flat figures would understate it. The OT row shows the OT and night-OT pay actually
    // given; the Night Diff. row shows the rest of the ND pay above its day-rate portion, over the
    // night hours that aren't OT (the night-OT hours are paid in full in the OT row).
    private sealed class Compounded : PayslipHoursAmounts
    {
        public override decimal Ot(PayCategoryFigures c) => c.OtPay + c.NdotPay;
        public override decimal NightDiffHours(PayCategoryFigures c) => Math.Max(0m, c.NdHours - c.NdotHours);
        public override decimal NightDiff(PayCategoryFigures c) => c.NdPay - c.NdBasePay;
    }
}
