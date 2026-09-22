using Hrms.Api.Documents;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Row math for the "Hours" payslip format, built from the client's own reference payslip (JDA
/// Security Services): DailyRate 540 (HourlyRate 67.50), Regular 96h (of which 72h are also
/// night-diff), Regular OT 48h @ 1.25, Special Holiday 8h @ 1.30, Special Holiday OT 4h @ 1.25,
/// Night Diff. premium 72h @ 0.10 -- Legal Holiday/Legal Holiday OT both zero and must not appear
/// as rows. Confirms BuildRows() reproduces the sample's own figures exactly and that
/// zero-amount categories are omitted (never rendered as a blank/zero line). Regular's 72 ND
/// hours fold their day-rate portion into "Regular Hours" (168h total) -- see
/// CategoryWithNightDifferentialHours_FoldsThemIntoTheBaseRow below for why: without this, a
/// guard working mostly at night would show understated hours, and the itemized total would fall
/// short of Gross Income by the ND hours' day-rate share.
/// </summary>
public class PayslipHoursDocumentTests
{
    private const decimal DailyRate = 540m;
    private const decimal HourlyRate = 67.5m; // DailyRate / 8

    private static Payroll BuildSamplePayroll() => new()
    {
        DailyRate = DailyRate,
        SalaryType = SalaryType.VARIABLE,
        RegularNetHours = 96,
        RegularOTHours = 48,
        RegularOTBasePay = 48 * HourlyRate * 1.25m, // 4,050.00
        SpecialHolHours = 8,
        SpecialPay = 8 * HourlyRate * 1.30m, // 702.00
        SpecialHolOTHours = 4,
        SpecialOTBasePay = 4 * HourlyRate * 1.25m, // 337.50
        RegularNDHours = 72,
        RegularNDBasePay = 72 * HourlyRate * 1.00m, // 4,860.00 -- day-rate portion, folded into Regular Hours
        RegularNDPremiumPay = 72 * HourlyRate * 0.10m, // 486.00 -- premium only, shown in Night Diff.
        GrossIncome = (96 * HourlyRate) + (72 * HourlyRate * 1.00m) + (48 * HourlyRate * 1.25m) + (8 * HourlyRate * 1.30m) + (4 * HourlyRate * 1.25m) + (72 * HourlyRate * 0.10m),
    };

    [Fact]
    public void SampleSpreadsheet_ReproducesRegularOTAndNightDiffRowsExactly()
    {
        var doc = new PayslipHoursDocument(BuildSamplePayroll(), new EmployeeFullModel());

        var rows = doc.BuildRows();

        var regular = rows.Single(r => r.Label == "Regular Hours");
        regular.Hours.Should().Be(168); // 96 plain + 72 ND, folded at the same day rate
        regular.Amount.Should().Be(11340.00m); // 6,480.00 + 4,860.00

        var regularOt = rows.Single(r => r.Label == "Regular OT Hours");
        regularOt.Hours.Should().Be(48);
        regularOt.Amount.Should().Be(4050.00m);

        var specialHoliday = rows.Single(r => r.Label == "Special Holiday");
        specialHoliday.Hours.Should().Be(8);
        specialHoliday.Amount.Should().Be(702.00m);

        var specialOt = rows.Single(r => r.Label == "Special Holiday OT");
        specialOt.Hours.Should().Be(4);
        specialOt.Amount.Should().Be(337.50m);

        var nightDiff = rows.Single(r => r.Label == "Night Diff.");
        nightDiff.Hours.Should().Be(72);
        nightDiff.Amount.Should().Be(486.00m);
    }

    [Fact]
    public void ZeroAmountCategories_AreOmittedEntirely_NeverRenderedAsBlankRows()
    {
        var doc = new PayslipHoursDocument(BuildSamplePayroll(), new EmployeeFullModel());

        var rows = doc.BuildRows();

        rows.Should().NotContain(r => r.Label.Contains("Legal Holiday"));
        rows.Should().NotContain(r => r.Label.Contains("Rest Day"));
        // 5 data rows: Regular Hours, Regular OT Hours, Special Holiday, Special Holiday OT,
        // Night Diff. -- the 4 group headers are emitted unconditionally regardless of content.
        rows.Count(r => !r.IsGroupHeader).Should().Be(5);
    }

    [Fact]
    public void FixedSalaryEmployee_RegularRowIsPlainBasicPay_NoHours()
    {
        var payroll = new Payroll
        {
            DailyRate = DailyRate,
            SalaryType = SalaryType.FIXED,
            BasicPay = 22500m,
            PaidLeaves = 0m,
            GrossIncome = 22500m,
        };
        var doc = new PayslipHoursDocument(payroll, new EmployeeFullModel());

        var rows = doc.BuildRows();

        var basicPay = rows.Single(r => r.Label == "Basic Pay");
        basicPay.ShowHours.Should().BeFalse();
        basicPay.Amount.Should().Be(22500m);
    }

    [Fact]
    public void CategoryWithNightDifferentialHours_FoldsThemIntoTheBaseRow()
    {
        // Regression: a guard who works mostly at night previously showed an understated
        // "Special Holiday" hours/amount, because SpecialHolNightDiffHours (that category's ND
        // hours) were only reflected in the merged "Night Diff." row's premium fraction, never
        // in the base row itself. SpecialNDBasePay already isolates the day-type-rate-only
        // portion of those same hours (no night premium, priced at the exact same rate as the
        // plain hours) -- it must be folded in so the base row's hours/amount reflect the
        // employee's FULL Special Holiday attendance, not just the non-ND slice of it.
        var payroll = new Payroll
        {
            DailyRate = DailyRate,
            SalaryType = SalaryType.VARIABLE,
            RegularNetHours = 56,
            RegularNDHours = 10,
            RegularNDBasePay = 10 * HourlyRate * 1.00m, // 675.00
            SpecialHolHours = 3,
            SpecialHolNightDiffHours = 2,
            SpecialPay = 3 * HourlyRate * 1.30m, // 263.25
            SpecialNDBasePay = 2 * HourlyRate * 1.30m, // 175.50
            GrossIncome = 0m,
        };
        var doc = new PayslipHoursDocument(payroll, new EmployeeFullModel());

        var rows = doc.BuildRows();

        var regular = rows.Single(r => r.Label == "Regular Hours");
        regular.Hours.Should().Be(66); // 56 plain + 10 ND
        regular.Amount.Should().Be(4455.00m); // 3,780.00 + 675.00

        var special = rows.Single(r => r.Label == "Special Holiday");
        special.Hours.Should().Be(5); // 3 plain + 2 ND
        special.Amount.Should().Be(438.75m); // 263.25 + 175.50
    }

    [Fact]
    public void AdditiveMode_TotalItemizedEqualsGrossIncomeExactly_ForACategoryWithAllFourHourBuckets()
    {
        // The whole point of Additive mode: unlike Compounded mode (where the itemized rows can
        // legitimately fall short of GROSS INCOME whenever a premium category has OT/ND hours),
        // Additive mode drops the day-type multiplier for OT-involving hours entirely, so
        // FlatOvertimeBase already equals the whole OT-bucket/NDOT-bucket contribution (same
        // formula Compounded mode has always used for these segregated fields) -- the itemized
        // rows (day-type row + "{Category} OT" row + merged "Night Diff." row) sum back to the
        // real total automatically, with no special folding needed. Numbers verified by hand
        // against OtNdCalculationMethodTests' formulas and confirmed against the company's own
        // manual payroll worksheet.
        const decimal dayRate = 1.30m, otRate = 1.25m, ndRate = 1.10m;
        var payroll = new Payroll
        {
            DailyRate = DailyRate,
            SalaryType = SalaryType.VARIABLE,
            OtNdCalculationMethod = OtNdCalculationMethod.Additive,

            SpecialHolHours = 8,
            SpecialPay = 8 * HourlyRate * dayRate, // 702.00

            SpecialHolOTHours = 4,
            SpecialOTBasePay = 4 * HourlyRate * otRate, // 337.50 -- raw OT rate alone, day rate dropped
            SpecialOTPay = 4 * HourlyRate * otRate, // 337.50 -- day rate doesn't apply to OT hours under Additive

            SpecialHolNightDiffHours = 5,
            SpecialNDBasePay = 5 * HourlyRate * dayRate, // 438.75
            SpecialNDPremiumPay = 5 * HourlyRate * (ndRate - 1.0m), // 33.75
            SpecialNDPay = 5 * HourlyRate * (dayRate + (ndRate - 1.0m)), // 472.50

            SpecialHolNightDiffOTHours = 3,
            SpecialNDOTBasePay = 3 * HourlyRate * otRate, // 253.125 -- raw OT rate alone, day rate dropped
            SpecialNDOTPremiumPay = 3 * HourlyRate * (ndRate - 1.0m), // 20.25
            SpecialNDOTPay = 3 * HourlyRate * (otRate + (ndRate - 1.0m)), // 273.375
        };
        payroll.GrossIncome = payroll.SpecialPay + payroll.SpecialOTPay + payroll.SpecialNDPay + payroll.SpecialNDOTPay; // 1,785.375

        var doc = new PayslipHoursDocument(payroll, new EmployeeFullModel());
        var rows = doc.BuildRows();

        var itemizedTotal = rows.Where(r => !r.IsGroupHeader).Sum(r => r.Amount);
        itemizedTotal.Should().Be(payroll.GrossIncome);

        var special = rows.Single(r => r.Label == "Special Holiday");
        special.Hours.Should().Be(13); // 8 plain + 5 ND -- OT/NDOT hours have no day-rate portion to fold in
        special.Amount.Should().Be(1140.75m);

        var otSpecial = rows.Single(r => r.Label == "Special Holiday OT");
        otSpecial.Hours.Should().Be(7); // 4 OT-only + 3 NDOT
        otSpecial.Amount.Should().Be(590.625m);

        var nightDiff = rows.Single(r => r.Label == "Night Diff.");
        nightDiff.Amount.Should().Be(54.00m); // 33.75 (ND) + 20.25 (NDOT)
    }

    [Fact]
    public void UnworkedLegalHoliday_MergesIntoOnePaidHolidayRow_DerivingHoursAtTheFixedMultiplier()
    {
        var payroll = new Payroll
        {
            DailyRate = DailyRate,
            SalaryType = SalaryType.VARIABLE,
            LegalHolidayUnworkedPay = 8 * HourlyRate * 1.00m, // a full 8h day, unworked, at 1.0x
            GrossIncome = 8 * HourlyRate,
        };
        var doc = new PayslipHoursDocument(payroll, new EmployeeFullModel());

        var rows = doc.BuildRows();

        var unworked = rows.Single(r => r.Label == "Paid Holiday (Unworked)");
        unworked.Hours.Should().Be(8);
        unworked.Amount.Should().Be(8 * HourlyRate);
    }

    [Fact]
    public void GroupHeaders_AlwaysEmitted_WithDayCountsOnWorkDayHeadersOnly()
    {
        var doc = new PayslipHoursDocument(BuildSamplePayroll(), new EmployeeFullModel());

        var rows = doc.BuildRows();

        rows.Where(r => r.IsGroupHeader).Select(r => r.Label).Should().Equal(
            "Total Number of Work Days", "Regular Days", "Holiday/s", "Other Income");
        rows.Single(r => r.Label == "Other Income").Hours.Should().Be(0);
    }
}
