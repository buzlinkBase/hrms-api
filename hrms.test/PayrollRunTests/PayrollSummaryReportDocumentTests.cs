using Hrms.Api.Documents;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Payroll Summary's Basic/OT/ND/Holiday columns must observe OtNdCalculationMethod the same
/// way PayslipHoursDocument already does. Hours are stored natural (OT and ND each already
/// include the NDOT hours), so the OT/ND hour columns read them as-is in both modes. Under
/// Additive mode: NDOT pay (both overtime AND night-diff) prices exactly like OT-only hours plus
/// a separate ND delta (day-type rate dropped entirely -- see Calculators/DtrPay), so it folds
/// into BOTH the OT and ND pay columns; and each category's ND-hours day-rate portion moves out of the ND
/// column into Basic (Regular) or Holiday (the 6 holiday categories) -- matching
/// PayslipHoursDocument.BaseRow's "Regular Hours"/"Special Holiday" fold exactly -- leaving ND
/// premium-only. Under Compounded mode, none of the pay folds apply: NDOT pay stays excluded from
/// OT/ND, and Basic/Holiday/ND all read their underlying Payroll fields unchanged, byte-for-byte as
/// before this feature.
/// </summary>
public class PayrollSummaryReportDocumentTests
{
    private static Payroll BuildRow(OtNdCalculationMethod method) => new()
    {
        OtNdCalculationMethod = method,

        // Regular category -- Basic's own scope.
        BasicPay = 1000m, // Regular's plain day-rate pay (RegularDayPay)
        RegularOTHours = 7, // natural: 5 daytime OT + 2 NDOT
        OvertimePay = 500m, // OT-only Value, mode-aware already
        RegularNDHours = 5, // natural: 3 night regular + 2 NDOT
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
    public void Compounded_LeavesBasicHolidayNdUnchanged_AndExcludesNdotPayFromOtAndNd()
    {
        var row = BuildRow(OtNdCalculationMethod.Compounded);

        PayrollSummaryReportDocument.OtHours(row).Should().Be(7); // natural, as stored
        PayrollSummaryReportDocument.NdHours(row).Should().Be(9); // Regular's 5 + Special's 4
        PayrollSummaryReportDocument.OtPay(row).Should().Be(500m);
        PayrollSummaryReportDocument.NdPay(row).Should().Be(85m); // full blended, unchanged
        PayrollSummaryReportDocument.BasicPay(row).Should().Be(1000m); // unchanged, no fold
        PayrollSummaryReportDocument.HolidayPay(row).Should().Be(200m); // unchanged, no fold
    }

    [Fact]
    public void Additive_FoldsNdotIntoOtAndNd_AndMovesNdDayRateIntoBasicAndHoliday()
    {
        var row = BuildRow(OtNdCalculationMethod.Additive);

        PayrollSummaryReportDocument.OtHours(row).Should().Be(7); // natural, as stored -- no fold
        PayrollSummaryReportDocument.NdHours(row).Should().Be(9); // Regular's 5 + Special's 4 -- no fold
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
    public void Additive_RunsFromTheSplitCalculator_ShowTheSameColumnsAsOlderAdditiveRuns()
    {
        // Same day as BuildRow, but stored the way AdditiveDtrPayCalculator (Calculators/DtrPay)
        // lays it out: ND hours' day-rate base already inside Basic/Holiday, ND buckets premium
        // only (NDOT's ND delta included), NDOT's OT portion already inside OvertimePay, and
        // every NDOT / ND-base field 0. The report's Additive fold must be a no-op on these, so
        // old and new runs read identically.
        var oldRow = BuildRow(OtNdCalculationMethod.Additive);
        var newRow = new Payroll
        {
            OtNdCalculationMethod = OtNdCalculationMethod.Additive,
            BasicPay = 1027m, // 1000 + Regular's ND base 27
            RegularOTHours = 7,
            OvertimePay = 625m, // 500 + NDOT's OT portion 125
            RegularNDHours = 5,
            RegularNDPremiumPay = 16.5m, // 3 + NDOT's ND delta 13.5
            RegularNDOTHours = 2,
            HolidayPay = 250m, // 200 + Special's ND base 50
            SpecialHolNightDiffHours = 4,
            SpecialNDPremiumPay = 5m,
            NightDifferentialPay = 21.5m,
        };

        PayrollSummaryReportDocument.OtHours(newRow).Should().Be(PayrollSummaryReportDocument.OtHours(oldRow));
        PayrollSummaryReportDocument.NdHours(newRow).Should().Be(PayrollSummaryReportDocument.NdHours(oldRow));
        PayrollSummaryReportDocument.OtPay(newRow).Should().Be(PayrollSummaryReportDocument.OtPay(oldRow));
        PayrollSummaryReportDocument.BasicPay(newRow).Should().Be(PayrollSummaryReportDocument.BasicPay(oldRow));
        PayrollSummaryReportDocument.HolidayPay(newRow).Should().Be(PayrollSummaryReportDocument.HolidayPay(oldRow));
        PayrollSummaryReportDocument.NdPay(newRow).Should().Be(PayrollSummaryReportDocument.NdPay(oldRow));
    }

    [Fact]
    public void Hours_ShowsTwoDecimals_SoMinuteLevelOtIsNotRoundedAway()
    {
        PayrollSummaryReportDocument.Hours(62m / 60m).Should().Be(1.03m.ToString("N2")); // 62 min OT
        PayrollSummaryReportDocument.Hours(8m).Should().Be(8m.ToString("N2"));
        PayrollSummaryReportDocument.Hours(0m).Should().Be("—");
    }

    [Fact]
    public void BasicHours_IsZeroForFixedSalary_NoHoursDriveAFlatMonthlyRate()
    {
        var fixedRow = new Payroll { SalaryType = SalaryType.FIXED, RegularNetHours = 96 };
        var variableRow = new Payroll { SalaryType = SalaryType.VARIABLE, RegularNetHours = 96 };

        PayrollSummaryReportDocument.BasicHours(fixedRow).Should().Be(0);
        PayrollSummaryReportDocument.BasicHours(variableRow).Should().Be(96);
    }

    [Fact]
    public void RestDayPay_SumsPlainRestDaysFourBuckets_LikeTheScreensRestDayColumn()
    {
        var row = new Payroll { RestDayPay = 100m, RestDayOTPay = 20m, RestDayNDPay = 5m, RestDayNDOTPay = 2m };

        PayrollSummaryReportDocument.RestDayPay(row).Should().Be(127m);
    }

    [Fact]
    public void PaidLeavePay_ShowsOnlyTheNonCompanySliceForFixed_AndTheFullAmountForVariable()
    {
        var fixedRow = new Payroll { SalaryType = SalaryType.FIXED, PaidLeaves = 1000m, NonCompanyPaidLeaves = 300m };
        var variableRow = new Payroll { SalaryType = SalaryType.VARIABLE, PaidLeaves = 1000m, NonCompanyPaidLeaves = 300m };

        PayrollSummaryReportDocument.PaidLeavePay(fixedRow).Should().Be(300m);
        PayrollSummaryReportDocument.PaidLeavePay(variableRow).Should().Be(1000m);
    }

    [Fact]
    public void OtherDeductions_ExcludeLoans_AndLateUndertimeCombinesBoth()
    {
        var row = new Payroll { OtherDeductions = 800m, TotalLoans = 500m, LateAmount = 40m, UnderTimeAmount = 15m };

        PayrollSummaryReportDocument.OtherDeductionsExcludingLoans(row).Should().Be(300m);
        PayrollSummaryReportDocument.LateUndertimeAmount(row).Should().Be(55m);
    }

    [Fact]
    public void Columns_IncludeEveryFigureTheScreenShows()
    {
        var doc = new PayrollSummaryReportDocument(new List<Payroll>(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));

        doc.ColumnHeaders.Should().Contain(new[]
        {
            "Rest Day", "Paid Leave", "COLA", "Bonuses", "Commissions", "De Minimis", "Reimb.",
            "Ret. Accrual", "Cash Bond", "Total Ded.", "Late/UT", "Absent", "Ret. Payout",
        });
        doc.ColumnHeaders.Should().OnlyHaveUniqueItems();
        doc.ColumnHeaders[0].Should().Be("Employee");
        doc.ColumnHeaders[^1].Should().Be("Net Pay");
    }
}
