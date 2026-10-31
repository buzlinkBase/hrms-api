using Hrms.Core.Services.Compliance;
using Hrms.Domain;

namespace hrms.test.ServiceTests;

/// <summary>
/// The DOLE-deviation rules beyond the OT/ND method, rates and client caps: which settings each one
/// flags, and that the statutory value of every one of them is not flagged.
/// </summary>
public class StatutoryDeviationRuleTests
{
    private static readonly EmployeeEligibility AllEligible = new(true, true, true, true, true);

    private static IEnumerable<string> Keys(IReadOnlyList<StatutoryDeviation> deviations) => deviations.Select(d => d.Key);

    // ── Company policy ───────────────────────────────────────────────────────

    [Fact]
    public void TheStatutoryCompanyPolicy_HasNoDeviations()
    {
        StatutoryDeviationRules.Evaluate(new CompanyPolicySubject(
            OtNdCalculationMethod.Compounded,
            HourlyRateDivisor.Fixed8Hours,
            OvertimeEligibilityRule.IndependentOfAttendanceIssues)).Should().BeEmpty();
    }

    [Fact]
    public void EachNonStatutoryCompanyPolicy_IsFlagged()
    {
        var deviations = StatutoryDeviationRules.Evaluate(new CompanyPolicySubject(
            OtNdCalculationMethod.Additive,
            HourlyRateDivisor.ShiftMaxWorkingHours,
            OvertimeEligibilityRule.OffsetAgainstUndertimeOrLateness,
            IsHalfDayLateOn: true,
            IsWholeDayLateOn: true,
            NightDiffThreshold: 30,
            RequiredTakehomePercentage: 10,
            DeductionCarrierMode: DeductionCarrierMode.Manual));

        Keys(deviations).Should().BeEquivalentTo(
            "policy.ot-nd-method", "policy.hourly-divisor", "policy.ot-eligibility", "policy.late-half-day",
            "policy.late-whole-day", "policy.nd-threshold", "policy.min-takehome", "policy.deduction-carrier");
    }

    [Fact]
    public void AClientOtEligibility_OffsetAgainstUndertime_IsFlagged_InheritingIsNot()
    {
        StatutoryDeviationRules.Evaluate(new OvertimeEligibilitySubject(OvertimeEligibilityRule.OffsetAgainstUndertimeOrLateness))
            .Should().ContainSingle().Which.Key.Should().Be("policy.ot-eligibility");
        StatutoryDeviationRules.Evaluate(new OvertimeEligibilitySubject(null)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(22.5, false)]
    [InlineData(30.0, false)]
    [InlineData(15.0, true)]
    public void ARetirementAccrual_BelowRa7641_IsFlagged(double? days, bool flagged)
    {
        var deviations = StatutoryDeviationRules.Evaluate(new ClientSubject((decimal?)days));
        if (flagged) deviations.Should().ContainSingle().Which.Key.Should().Be("client.retirement");
        else deviations.Should().BeEmpty();
    }

    // ── Employee ─────────────────────────────────────────────────────────────

    [Fact]
    public void AnEmployeeOnTheTables_EligibleForEverything_AboveMinimumWage_HasNoDeviations()
    {
        StatutoryDeviationRules.Evaluate(new EmployeePaySubject(
            ComputationBasis.Table, ComputationBasis.Table, ComputationBasis.Table, ComputationBasis.Table,
            AllEligible, DailyRate: 700, RegionalMinimumDailyRate: 645)).Should().BeEmpty();
    }

    [Fact]
    public void ContributionsOffOrFixed_AreFlaggedPerContribution()
    {
        var deviations = StatutoryDeviationRules.Evaluate(new EmployeePaySubject(
            Sss: ComputationBasis.None, PhilHealth: ComputationBasis.FixedPerPayroll));

        Keys(deviations).Should().BeEquivalentTo("employee.sss.method", "employee.philhealth.method");
        deviations.Single(d => d.Key == "employee.sss.method").ConfiguredValue.Should().Be("None");
    }

    [Fact]
    public void ExcludingAMandatoryBenefit_IsFlagged()
    {
        var deviations = StatutoryDeviationRules.Evaluate(new EmployeePaySubject(
            Eligibility: AllEligible with { ThirteenthMonth = false, RegularHolidayPay = false }));

        Keys(deviations).Should().BeEquivalentTo("employee.eligibility.13th-month", "employee.eligibility.regular-holiday");
    }

    [Theory]
    [InlineData(600.0, 645.0, true)]
    [InlineData(645.0, 645.0, false)]
    [InlineData(600.0, null, false)] // no region set up for the branch: nothing to compare against
    [InlineData(0.0, 645.0, false)]    // pay hidden or not sent
    public void ADailyRateBelowTheRegionalMinimum_IsFlagged(double rate, double? minimum, bool flagged)
    {
        var deviations = StatutoryDeviationRules.Evaluate(new EmployeePaySubject(
            DailyRate: (decimal)rate, RegionalMinimumDailyRate: (decimal?)minimum));
        if (flagged) deviations.Should().ContainSingle().Which.Key.Should().Be("employee.below-minimum-wage");
        else deviations.Should().BeEmpty();
    }

    [Fact]
    public void Distinct_KeepsOneDeviationPerSettingAndValue()
    {
        var none = StatutoryDeviationRules.Evaluate(new EmployeePaySubject(Sss: ComputationBasis.None));
        var fixedRate = StatutoryDeviationRules.Evaluate(new EmployeePaySubject(Sss: ComputationBasis.FixedPerPayroll));

        StatutoryDeviationRules.Distinct([.. none, .. none, .. fixedRate]).Should().HaveCount(2);
    }

    // ── Time shift ───────────────────────────────────────────────────────────

    [Fact]
    public void AShiftThatPaysAllOtAndNightWork_WithPaidShortBreaks_HasNoDeviations()
    {
        StatutoryDeviationRules.Evaluate(new TimeShiftSubject(0, null, null, BreakMode.PAID_BREAK, BreakMode.NONE))
            .Should().BeEmpty();
    }

    [Fact]
    public void ShiftThresholdsCapsAndUnpaidShortBreaks_AreFlagged()
    {
        var deviations = StatutoryDeviationRules.Evaluate(
            new TimeShiftSubject(60, 4, 30, BreakMode.UNPAID_BREAK, BreakMode.PAID_BREAK));

        Keys(deviations).Should().BeEquivalentTo(
            "shift.ot-threshold", "shift.ot-cap", "shift.nd-threshold", "shift.unpaid-short-break");
    }

    // ── Leave ────────────────────────────────────────────────────────────────

    private static LeaveSubject Sil(
        AccrualBasis basis = AccrualBasis.Annually, double rate = 5, int months = 12, bool cash = true, decimal cashRate = 1m) =>
        new("SIL", basis, 0, rate, months, cash, cashRate);

    [Fact]
    public void TheSeededSil_HasNoDeviations()
    {
        StatutoryDeviationRules.Evaluate(Sil()).Should().BeEmpty();
        StatutoryDeviationRules.Evaluate(Sil(AccrualBasis.Monthly, 5.0 / 12)).Should().BeEmpty();
    }

    [Fact]
    public void ASilBelowArt95_IsFlagged()
    {
        Keys(StatutoryDeviationRules.Evaluate(Sil(rate: 3, months: 24, cash: false))).Should().BeEquivalentTo(
            "leave.sil.days", "leave.sil.service", "leave.sil.conversion");
        Keys(StatutoryDeviationRules.Evaluate(Sil(cashRate: 0.5m))).Should().Equal("leave.sil.conversion");
    }

    [Fact]
    public void OtherLeaveTypes_AreNotChecked()
    {
        StatutoryDeviationRules.Evaluate(new LeaveSubject("VL", AccrualBasis.None, 0, 0, 36, false, 0))
            .Should().BeEmpty();
    }
}
