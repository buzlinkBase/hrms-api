using Hrms.Api.Documents;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Payroll Summary's Basic/OT/ND/Holiday columns must observe OtNdCalculationMethod the same
/// way PayslipHoursDocument already does. Under Additive mode: NDOT hours (both overtime AND
/// night-diff) price exactly like OT-only hours plus a separate ND delta (day-type rate dropped
/// entirely -- see OvertimeCategoryPolicies/NightDiffOTCategoryPolicies), so they fold into BOTH
/// the OT and ND columns; and each category's ND-hours day-rate portion moves out of the ND
/// column into Basic (Regular) or Holiday (the 6 holiday categories) -- matching
/// PayslipHoursDocument.BaseRow's "Regular Hours"/"Special Holiday" fold exactly -- leaving ND
/// premium-only. Under Compounded mode, none of this applies: NDOT stays excluded from OT/ND,
/// and Basic/Holiday/ND all read their underlying Payroll fields unchanged, byte-for-byte as
/// before this feature.
/// </summary>
public class PayrollSummaryReportDocumentTests
{
    private static Payroll BuildRow(OtNdCalculationMethod method) => new()
    {
        OtNdCalculationMethod = method,

        // Regular category -- Basic's own scope.
        BasicPay = 1000m, // Regular's plain day-rate pay (RegularDayPay)
        RegularOTHours = 5,
        OvertimePay = 500m, // OT-only Value, mode-aware already
        RegularNDHours = 3,
        RegularNDBasePay = 27m, // ND-only day-rate portion
        RegularNDPremiumPay = 3m, // ND-only premium portion (27 + 3 = 30, folded into NightDifferentialPay below)
        RegularNDOTHours = 2,
        RegularNDOTBasePay = 125m, // NDOT's raw-OT-rate portion (FlatOvertimeBase)
        RegularNDOTPremiumPay = 13.5m, // NDOT's ND-delta portion (FlatNightDiffPremium)

        // Special Holiday category -- Holiday's own scope.
        HolidayPay = 200m, // Plain day-rate pay for holiday categories
        SpecialHolNightDiffHours = 4,
        SpecialNDBasePay = 50m, // ND-only day-rate portion for Special
        SpecialNDPremiumPay = 5m, // ND-only premium portion for Special (50 + 5 = 55)

        // Full blended ND-only Value summed across all 8 categories, matching
        // Sum(TotalND)'s real-world scope: Regular's 30 (27+3) + Special's 55 (50+5).
        NightDifferentialPay = 85m,
    };

    [Fact]
    public void Compounded_LeavesBasicHolidayNdUnchanged_AndExcludesNdotFromOtAndNd()
    {
        var row = BuildRow(OtNdCalculationMethod.Compounded);

        PayrollSummaryReportDocument.OtHours(row).Should().Be(5);
        PayrollSummaryReportDocument.NdHours(row).Should().Be(7); // Regular's 3 + Special's 4
        PayrollSummaryReportDocument.OtPay(row).Should().Be(500m);
        PayrollSummaryReportDocument.NdPay(row).Should().Be(85m); // full blended, unchanged
        PayrollSummaryReportDocument.BasicPay(row).Should().Be(1000m); // unchanged, no fold
        PayrollSummaryReportDocument.HolidayPay(row).Should().Be(200m); // unchanged, no fold
    }

    [Fact]
    public void Additive_FoldsNdotIntoOtAndNd_AndMovesNdDayRateIntoBasicAndHoliday()
    {
        var row = BuildRow(OtNdCalculationMethod.Additive);

        PayrollSummaryReportDocument.OtHours(row).Should().Be(7); // 5 OT-only + 2 NDOT
        PayrollSummaryReportDocument.NdHours(row).Should().Be(9); // (Regular's 3 + Special's 4) + 2 NDOT
        PayrollSummaryReportDocument.OtPay(row).Should().Be(625m); // 500 + 125 (NDOT's OT-rate portion)
        PayrollSummaryReportDocument.BasicPay(row).Should().Be(1027m); // 1000 + 27 (Regular's ND day-rate)
        PayrollSummaryReportDocument.HolidayPay(row).Should().Be(250m); // 200 + 50 (Special's ND day-rate)
        // Premium-only now: Regular's 3 + Special's 5 + NDOT's 13.5 = 21.5 (day-rate portions
        // moved to Basic/Holiday above instead of staying here).
        PayrollSummaryReportDocument.NdPay(row).Should().Be(21.5m);

        // The whole point: rebalancing Basic/Holiday/ND must not change their combined total --
        // it only moves money between columns. Old (unfolded) NdPay would have been
        // 85 (full blended) + 13.5 (NDOT's ND delta, already folded in before this change) =
        // 98.5, so 1000+200+98.5 (old) == 1027+250+21.5 (new).
        (PayrollSummaryReportDocument.BasicPay(row) + PayrollSummaryReportDocument.HolidayPay(row) + PayrollSummaryReportDocument.NdPay(row))
            .Should().Be(1000m + 200m + 98.5m);
    }

    [Fact]
    public void Additive_LeavesPlainRestDayNdFullyInTheNdColumn_NoFoldDestinationForIt()
    {
        // Rest Day has no Basic/Holiday-style column of its own to fold ND day-rate into (its
        // full Value already lives entirely in the separate "Rest Day" column upstream), so its
        // ND bucket must stay untouched here -- both its day-rate and premium portions remain
        // inside NdPay, unlike Regular/holiday categories.
        var row = BuildRow(OtNdCalculationMethod.Additive);
        row.RestDayNDPay = 40m; // full blended Rest Day ND value (day-rate + premium together)

        PayrollSummaryReportDocument.NdPay(row).Should().Be(21.5m + 40m);
    }

    [Fact]
    public void BasicHours_IsZeroForFixedSalary_NoHoursDriveAFlatMonthlyRate()
    {
        var fixedRow = new Payroll { SalaryType = SalaryType.FIXED, RegularNetHours = 96 };
        var variableRow = new Payroll { SalaryType = SalaryType.VARIABLE, RegularNetHours = 96 };

        PayrollSummaryReportDocument.BasicHours(fixedRow).Should().Be(0);
        PayrollSummaryReportDocument.BasicHours(variableRow).Should().Be(96);
    }
}
