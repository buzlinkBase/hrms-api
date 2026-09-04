using Hrms.Core.Policies.DTRPolicies;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// DayTypePayCalculator replaces six near-identical XxxPayCalculator classes (Regular/Special
/// Holiday, their Rest-Day combos, Double Legal Holiday and its Rest-Day combo) that all
/// implemented the same worked/unworked pricing formula by hand. Made `internal` (not
/// private) specifically so it's testable here without a database, matching
/// PayrollLineComputationTests' convention — see Hrms.Core's InternalsVisibleTo for hrms.test.
/// </summary>
public class DayTypePayCalculatorTests
{
    private const decimal HourlyRate = 100m;

    [Fact]
    public void CalculateWorkedPay_Ineligible_PaysFlatBaseRateRegardlessOfMultiplierOrPreFunding()
    {
        var calc = DayTypePayCalculator.For(isEligible: false, isBasePayPreFunded: true, workedMultiplier: 2.0m, unworkedMultiplier: 1.0m);

        calc.CalculateWorkedPay(HourlyRate, workedHours: 8);

        calc.Total.Should().Be(HourlyRate * 8 * 1.0m);
    }

    [Fact]
    public void CalculateWorkedPay_EligibleNotPreFunded_PaysFullMultiplier()
    {
        var calc = DayTypePayCalculator.For(isEligible: true, isBasePayPreFunded: false, workedMultiplier: 2.0m, unworkedMultiplier: 1.0m);

        calc.CalculateWorkedPay(HourlyRate, workedHours: 8);

        calc.Total.Should().Be(HourlyRate * 8 * 2.0m);
    }

    [Fact]
    public void CalculateWorkedPay_EligiblePreFunded_PaysOnlyDeltaAboveBase()
    {
        // Fixed-salary employee whose base pay already covers the 1.0x portion — only the
        // premium delta (2.0 - 1.0 = 1.0) is owed on top.
        var calc = DayTypePayCalculator.For(isEligible: true, isBasePayPreFunded: true, workedMultiplier: 2.0m, unworkedMultiplier: 1.0m);

        calc.CalculateWorkedPay(HourlyRate, workedHours: 8);

        calc.Total.Should().Be(HourlyRate * 8 * 1.0m);
    }

    [Fact]
    public void CalculateWorkedPay_PreFundedWithMultiplierBelowBase_ClampsToZeroInsteadOfNegative()
    {
        var calc = DayTypePayCalculator.For(isEligible: true, isBasePayPreFunded: true, workedMultiplier: 0.8m, unworkedMultiplier: 1.0m);

        calc.CalculateWorkedPay(HourlyRate, workedHours: 8);

        calc.Total.Should().Be(0m);
    }

    [Fact]
    public void CalculateWorkedPay_ZeroHours_ContributesNothing()
    {
        var calc = DayTypePayCalculator.For(isEligible: true, isBasePayPreFunded: false, workedMultiplier: 2.0m, unworkedMultiplier: 1.0m);

        calc.CalculateWorkedPay(HourlyRate, workedHours: 0);

        calc.Total.Should().Be(0m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CalculateUnworkedPay_NoWorkNoPayDayType_AlwaysZero_RegardlessOfPreFunding(bool isBasePayPreFunded)
    {
        // Special Non-Working Holiday and its Rest-Day combo pass unworkedMultiplier: 0m —
        // DOLE's "no work, no pay" rule needs no separate branch to enforce.
        var calc = DayTypePayCalculator.For(isEligible: true, isBasePayPreFunded, workedMultiplier: 1.3m, unworkedMultiplier: 0m);

        calc.CalculateUnworkedPay(HourlyRate, unworkedHours: 8);

        calc.Total.Should().Be(0m);
    }

    [Fact]
    public void CalculateUnworkedPay_EligibleNotPreFunded_PaysFullUnworkedMultiplier()
    {
        var calc = DayTypePayCalculator.For(isEligible: true, isBasePayPreFunded: false, workedMultiplier: 3.0m, unworkedMultiplier: 2.0m);

        calc.CalculateUnworkedPay(HourlyRate, unworkedHours: 8);

        calc.Total.Should().Be(HourlyRate * 8 * 2.0m);
    }

    [Fact]
    public void CalculateUnworkedPay_EligiblePreFunded_PaysOnlyDeltaAboveBase()
    {
        // Matches DoubleLegalPolicy/RestDoubleLegalPolicy: unworked double holiday is 2.0x;
        // base pay covers 1.0x, so only the 1.0x delta remains due.
        var calc = DayTypePayCalculator.For(isEligible: true, isBasePayPreFunded: true, workedMultiplier: 3.0m, unworkedMultiplier: 2.0m);

        calc.CalculateUnworkedPay(HourlyRate, unworkedHours: 8);

        calc.Total.Should().Be(HourlyRate * 8 * 1.0m);
    }

    [Fact]
    public void CalculateUnworkedPay_Ineligible_ContributesNothing()
    {
        var calc = DayTypePayCalculator.For(isEligible: false, isBasePayPreFunded: false, workedMultiplier: 2.0m, unworkedMultiplier: 1.0m);

        calc.CalculateUnworkedPay(HourlyRate, unworkedHours: 8);

        calc.Total.Should().Be(0m);
    }

    [Fact]
    public void PartialDayAttendance_WorkedAndUnworkedBothPresent_LineReflectsEachStageAmountSeparately()
    {
        // Regression: the old per-policy calculators set line.UnWork to the *cumulative*
        // running total (worked + unworked) instead of just the unworked portion, so any
        // day with both worked and unworked hours (e.g. clocked in 4 of 8 hours on a legal
        // holiday) overstated UnWork. Each stage must report only its own amount.
        var line = new BasicPipelineData();
        var calc = DayTypePayCalculator.For(isEligible: true, isBasePayPreFunded: false, workedMultiplier: 2.0m, unworkedMultiplier: 1.0m, line);

        calc.CalculateWorkedPay(HourlyRate, workedHours: 4)
            .CalculateUnworkedPay(HourlyRate, unworkedHours: 4);

        var expectedWorked = HourlyRate * 4 * 2.0m;
        var expectedUnworked = HourlyRate * 4 * 1.0m;

        line.Worked.Should().Be(expectedWorked);
        line.UnWork.Should().Be(expectedUnworked);
        calc.Total.Should().Be(expectedWorked + expectedUnworked);
    }

    [Fact]
    public void NoLineSupplied_DoesNotThrow_AndStillAccumulatesTotal()
    {
        var calc = DayTypePayCalculator.For(isEligible: true, isBasePayPreFunded: false, workedMultiplier: 2.0m, unworkedMultiplier: 1.0m);

        calc.CalculateWorkedPay(HourlyRate, workedHours: 8)
            .CalculateUnworkedPay(HourlyRate, unworkedHours: 0);

        calc.Total.Should().Be(HourlyRate * 8 * 2.0m);
    }
}
