using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Payroll-only (no DTR) employees -- owners, shareholders, officers: NoDtrWindow decides when the
/// flag is in force, how much of a cutoff it covers (proration) and which combinations are valid;
/// GetBasicPay pays the flat fixed rate, prorated by that coverage.
/// </summary>
public class NoDtrWindowTests
{
    private static readonly DateOnly CutoffFrom = new(2026, 8, 1);
    private static readonly DateOnly CutoffTo = new(2026, 8, 15);

    private static CreateEmployeeSetting Window(bool flag, DateOnly? from, DateOnly? until) => new()
    {
        IsNoDTRNotRequired = flag,
        NoDtrEffectiveFrom = from,
        NoDtrEffectiveUntil = until,
    };

    // ── IsActiveOn ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void IsActiveOn_FlagOff_IsNeverActive() =>
        NoDtrWindow.IsActiveOn(Window(false, null, null), CutoffFrom).Should().BeFalse();

    [Fact]
    public void IsActiveOn_OpenEnded_IsActiveForAnyLaterDate()
    {
        var s = Window(true, new DateOnly(2026, 1, 1), null);
        NoDtrWindow.IsActiveOn(s, new DateOnly(2030, 1, 1)).Should().BeTrue();
        NoDtrWindow.IsActiveOn(s, new DateOnly(2025, 12, 31)).Should().BeFalse();
    }

    [Fact]
    public void IsActiveOn_Bounded_IncludesBothEndDates()
    {
        var s = Window(true, new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 10));
        NoDtrWindow.IsActiveOn(s, new DateOnly(2026, 8, 4)).Should().BeFalse();
        NoDtrWindow.IsActiveOn(s, new DateOnly(2026, 8, 5)).Should().BeTrue();
        NoDtrWindow.IsActiveOn(s, new DateOnly(2026, 8, 10)).Should().BeTrue();
        NoDtrWindow.IsActiveOn(s, new DateOnly(2026, 8, 11)).Should().BeFalse();
    }

    // ── CoverageFactor ───────────────────────────────────────────────────────────────────

    [Fact]
    public void CoverageFactor_OpenEndedAndNoStart_CoversWholeCutoff() =>
        NoDtrWindow.CoverageFactor(Window(true, null, null), CutoffFrom, CutoffTo).Should().Be(1m);

    [Fact]
    public void CoverageFactor_StartsMidCutoff_CoversRemainingDays() =>
        // Aug 9-15 = 7 of the 15 days
        NoDtrWindow.CoverageFactor(Window(true, new DateOnly(2026, 8, 9), null), CutoffFrom, CutoffTo)
            .Should().Be(7m / 15m);

    [Fact]
    public void CoverageFactor_EndsMidCutoff_CoversLeadingDays() =>
        // Aug 1-5 = 5 of the 15 days
        NoDtrWindow.CoverageFactor(Window(true, null, new DateOnly(2026, 8, 5)), CutoffFrom, CutoffTo)
            .Should().Be(5m / 15m);

    [Fact]
    public void CoverageFactor_WindowOutsideCutoff_IsZero()
    {
        NoDtrWindow.CoverageFactor(Window(true, new DateOnly(2026, 9, 1), null), CutoffFrom, CutoffTo).Should().Be(0m);
        NoDtrWindow.CoverageFactor(Window(true, null, new DateOnly(2026, 7, 31)), CutoffFrom, CutoffTo).Should().Be(0m);
    }

    [Fact]
    public void CoverageFactor_FlagOffOrNoSettings_IsZero()
    {
        NoDtrWindow.CoverageFactor(Window(false, null, null), CutoffFrom, CutoffTo).Should().Be(0m);
        NoDtrWindow.CoverageFactor(null, CutoffFrom, CutoffTo).Should().Be(0m);
    }

    // ── Validate ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_FlagOff_AlwaysValid() =>
        NoDtrWindow.Validate(false, null, null, null, SalaryType.VARIABLE).Should().BeNull();

    [Fact]
    public void Validate_VariableSalary_IsRejected() =>
        NoDtrWindow.Validate(true, NoDtrCategory.OFFICER, null, null, SalaryType.VARIABLE).Should().NotBeNull();

    [Fact]
    public void Validate_MissingCategory_IsRejected() =>
        NoDtrWindow.Validate(true, null, null, null, SalaryType.FIXED).Should().NotBeNull();

    [Fact]
    public void Validate_UntilBeforeFrom_IsRejected() =>
        NoDtrWindow.Validate(true, NoDtrCategory.DIRECTOR, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 9), SalaryType.FIXED)
            .Should().NotBeNull();

    [Fact]
    public void Validate_FixedWithCategoryAndOpenEnd_IsValid() =>
        NoDtrWindow.Validate(true, NoDtrCategory.OWNER_SHAREHOLDER, new DateOnly(2026, 1, 1), null, SalaryType.FIXED)
            .Should().BeNull();

    // ── GetBasicPay with no DTR ──────────────────────────────────────────────────────────

    private static EmployeeModelPayrollRun FixedEmployee() => new()
    {
        Id = Guid.NewGuid(),
        SalaryType = SalaryType.FIXED,
        MonthlyRate = 30_000,
        PayrollGroup = new PayrollGroupModel { PayrollFrequency = PayrollFrequency.SEMI_MONTHLY },
    };

    [Fact]
    public void GetBasicPay_NoDtrFullCoverage_PaysFullPeriodShare()
    {
        var line = new PayrollSummaryLine { PayPeriodStart = CutoffFrom };
        EmployeePayrollLineService.GetBasicPay(line, new List<DTRPayModel>(), FixedEmployee(), 0, payrollOnlyFactor: 1m);
        line.BasicPay.Should().Be(15_000);
    }

    [Fact]
    public void GetBasicPay_NoDtrPartialCoverage_ProratesByCalendarDays()
    {
        var line = new PayrollSummaryLine { PayPeriodStart = CutoffFrom };
        EmployeePayrollLineService.GetBasicPay(line, new List<DTRPayModel>(), FixedEmployee(), 0, payrollOnlyFactor: 7m / 15m);
        // 30,000 / 2 * 7/15
        line.BasicPay.Should().Be(7_000);
    }
}
