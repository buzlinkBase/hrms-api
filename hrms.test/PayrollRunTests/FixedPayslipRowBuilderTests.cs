using Hrms.Api.Documents.Fixed;
using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// FixedPayslipRowBuilder -- the Earnings rows of the Fixed-salary payslip. A Fixed employee is
/// paid a flat period share of the monthly rate, so the rows must start from Basic Salary, show
/// the attendance cuts against it, and label holiday/rest-day lines "Premium" vs "Pay" from the
/// inclusion snapshot recorded on the Payroll row.
/// </summary>
public class FixedPayslipRowBuilderTests
{
    private static Payroll FixedPayroll() => new()
    {
        SalaryType = SalaryType.FIXED,
        PayrollType = PayrollType.Regular,
        FixedBasicSalary = 15_000m,
        LateAmount = 100m,
        UnderTimeAmount = 50m,
        AbsencesAmount = 1_000m,
        UnpaidLeaves = 500m,
        BasicPay = 13_350m,
    };

    private static IReadOnlyList<FixedPayslipRow> Rows(Payroll p) => new FixedPayslipRowBuilder(p).Build();

    private static FixedPayslipRow? Find(IReadOnlyList<FixedPayslipRow> rows, string label) =>
        rows.Cast<FixedPayslipRow?>().FirstOrDefault(r => r!.Value.Label == label);

    [Fact]
    public void BasicSalary_ThenEachAttendanceCut_ThenNetBasicPay_ReconcilesToBasicPay()
    {
        var rows = Rows(FixedPayroll());

        Find(rows, "Basic Salary")!.Value.Amount.Should().Be(15_000m);
        Find(rows, "Late")!.Value.Kind.Should().Be(FixedPayslipRowKind.Cut);
        Find(rows, "Late")!.Value.Amount.Should().Be(100m);
        Find(rows, "Under Time")!.Value.Amount.Should().Be(50m);
        Find(rows, "Absences")!.Value.Amount.Should().Be(1_000m);
        Find(rows, "Unpaid Leave")!.Value.Amount.Should().Be(500m);
        Find(rows, "Net Basic Pay")!.Value.Amount.Should().Be(13_350m);

        var cuts = rows.Where(r => r.Kind == FixedPayslipRowKind.Cut).Sum(r => r.Amount);
        (15_000m - cuts).Should().Be(13_350m);
    }

    [Fact]
    public void ZeroAmountLines_AreOmitted()
    {
        var p = FixedPayroll();
        p.LateAmount = 0m;
        p.UnderTimeAmount = 0m;
        p.AbsencesAmount = 0m;
        p.UnpaidLeaves = 0m;
        p.BasicPay = 15_000m;

        var rows = Rows(p);

        rows.Should().NotContain(r => r.Kind == FixedPayslipRowKind.Cut);
        rows.Should().NotContain(r => r.Label == "Late");
    }

    [Fact]
    public void OldRowWithoutSnapshot_RebuildsBasicSalaryFromBasicPayPlusCuts()
    {
        var p = FixedPayroll();
        p.FixedBasicSalary = 0m;

        Find(Rows(p), "Basic Salary")!.Value.Amount.Should().Be(15_000m);
    }

    [Fact]
    public void BasicPayShortOfBasicMinusCuts_WithOneTimeLeave_ShowsOneTimeLeaveCut()
    {
        var p = FixedPayroll();
        p.BasicPay = 10_350m; // 3,000 less than Basic - cuts
        p.OneTimePayoutBreakdown = "Maternity Leave: 3,000.00 (Government)";

        var rows = Rows(p);

        Find(rows, "One-Time Leave (paid as lump sum)")!.Value.Amount.Should().Be(3_000m);
    }

    [Fact]
    public void BasicPayAboveBasicMinusCuts_ShowsSalaryAdjustmentEarning()
    {
        var p = FixedPayroll();
        p.BasicPay = 13_850m; // 500 salary adjustment added after the fact

        var adjustment = Find(Rows(p), "Salary Adjustment")!.Value;

        adjustment.Kind.Should().Be(FixedPayslipRowKind.Earning);
        adjustment.Amount.Should().Be(500m);
    }

    [Fact]
    public void LegalHoliday_IncludedInMonthlyRate_IsAPremium_AndHasNoUnworkedLine()
    {
        var p = FixedPayroll();
        p.IsRegularHolidayIncluded = true;
        p.LegalPay = 1_000m; // delta premium only; unworked is pre-funded, so 0

        var rows = Rows(p);

        Find(rows, "Legal Holiday Premium (Worked)")!.Value.Amount.Should().Be(1_000m);
        rows.Should().NotContain(r => r.Label.Contains("(Unworked)"));
    }

    [Fact]
    public void LegalHoliday_NotIncluded_IsFullPay_SplitIntoWorkedAndUnworked()
    {
        var p = FixedPayroll();
        p.IsRegularHolidayIncluded = false;
        p.LegalPay = 3_000m;              // worked 2,000 + unworked 1,000, blended
        p.LegalHolidayUnworkedPay = 1_000m;

        var rows = Rows(p);

        Find(rows, "Legal Holiday Pay (Worked)")!.Value.Amount.Should().Be(2_000m);
        Find(rows, "Legal Holiday Pay (Unworked)")!.Value.Amount.Should().Be(1_000m);
    }

    [Fact]
    public void SpecialHoliday_LabelFollowsItsOwnFlag_NotTheRegularHolidayFlag()
    {
        var p = FixedPayroll();
        p.IsRegularHolidayIncluded = true;
        p.IsSpecialNonWorkingIncluded = false;
        p.SpecialPay = 800m;

        var rows = Rows(p);

        Find(rows, "Special Holiday Pay")!.Value.Amount.Should().Be(800m);
        Find(rows, "Special Holiday Premium").Should().BeNull();
    }

    [Fact]
    public void RestDayCombos_NeedBothFlagsToBeAPremium()
    {
        var p = FixedPayroll();
        p.IsRestDayPaid = true;
        p.IsRegularHolidayIncluded = false;
        p.RestLegalPay = 900m;
        p.RestDayPay = 400m;

        var rows = Rows(p);

        Find(rows, "Rest Day Premium")!.Value.Amount.Should().Be(400m);
        Find(rows, "Rest Day + Legal Holiday Pay (Worked)")!.Value.Amount.Should().Be(900m);
    }

    [Fact]
    public void OvertimeAndNightDifferential_UsePeriodTotals_AndEachSectionGetsAHeaderOnlyWhenItHasRows()
    {
        var p = FixedPayroll();
        p.OvertimePay = 600m;
        p.NightDifferentialPay = 120m;
        p.NightDifferentialOTPay = 30m;

        var rows = Rows(p);

        Find(rows, "Overtime")!.Value.Amount.Should().Be(600m);
        Find(rows, "Night Differential")!.Value.Amount.Should().Be(120m);
        Find(rows, "Night Differential Overtime")!.Value.Amount.Should().Be(30m);
        rows.Should().Contain(r => r.Kind == FixedPayslipRowKind.Header && r.Label == "OVERTIME & NIGHT DIFFERENTIAL");
        rows.Should().NotContain(r => r.Kind == FixedPayslipRowKind.Header && r.Label == "HOLIDAY");
        rows.Should().NotContain(r => r.Kind == FixedPayslipRowKind.Header && r.Label == "REST DAY");
    }

    [Fact]
    public void PaidLeave_IsAnInformationalNote_NotAnEarning()
    {
        var p = FixedPayroll();
        p.PaidLeaves = 1_000m;

        var note = Find(Rows(p), "Paid Leave (included in Net Basic Pay)")!.Value;

        note.Kind.Should().Be(FixedPayslipRowKind.Note);
    }

    [Fact]
    public void EarningRowsAndCuts_ReconcileToGrossIncome()
    {
        var p = FixedPayroll();
        p.IsRegularHolidayIncluded = true;
        p.LegalPay = 1_000m;
        p.RestDayPay = 400m;
        p.OvertimePay = 600m;
        p.NightDifferentialPay = 120m;
        p.Cola = 300m;
        p.TotalOtherIncome = 200m;
        p.GrossIncome = 13_350m + 1_000m + 400m + 600m + 120m + 300m + 200m;

        var rows = Rows(p);

        // Net Basic Pay is the subtotal of Basic - cuts, so it replaces the Basic/cut lines when
        // summing; every other Earning row adds on top.
        var gross = rows.Where(r => r.Kind == FixedPayslipRowKind.Subtotal).Sum(r => r.Amount)
            + rows.Where(r => r.Kind == FixedPayslipRowKind.Earning && r.Label != "Basic Salary").Sum(r => r.Amount);

        gross.Should().Be(p.GrossIncome);
    }

    [Fact]
    public void GetBasicPay_RecordsFixedBasicSalary_BeforeAttendanceCuts()
    {
        var employee = new EmployeeModelPayrollRun
        {
            Id = Guid.NewGuid(),
            SalaryType = SalaryType.FIXED,
            MonthlyRate = 30_000,
            DailyRate = 1_000,
            PayrollGroup = new PayrollGroupModel { PayrollFrequency = PayrollFrequency.SEMI_MONTHLY },
        };
        var line = new PayrollSummaryLine { PayPeriodStart = new DateOnly(2026, 8, 1), PayPeriodEnd = new DateOnly(2026, 8, 15) };
        var days = new List<DTRPayModel> { new() { LateAmount = 100m, AbsentAmount = 1_000m } };

        EmployeePayrollLineService.GetBasicPay(line, days, employee, oneTimeLeaveDays: 0);

        line.FixedBasicSalary.Should().Be(15_000m);
        line.BasicPay.Should().Be(13_900m);
    }
}
