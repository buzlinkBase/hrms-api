using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// PayrollProcessorService.ComputeThirteenthMonthTaxSplit — splits a 13th month gross into
/// the non-taxable portion (up to the exemption ceiling, default 90,000 per TRAIN law) and
/// the taxable excess above it. See GenerateThirteenthMonthAsync.
/// </summary>
public class ThirteenthMonthTaxSplitTests
{
    [Fact]
    public void GrossUnderCeiling_IsFullyNonTaxable()
    {
        var (nonTaxable, taxable) = PayrollProcessorService.ComputeThirteenthMonthTaxSplit(gross: 60_000, ceiling: 90_000);

        nonTaxable.Should().Be(60_000);
        taxable.Should().Be(0);
    }

    [Fact]
    public void GrossExactlyAtCeiling_IsFullyNonTaxable()
    {
        var (nonTaxable, taxable) = PayrollProcessorService.ComputeThirteenthMonthTaxSplit(gross: 90_000, ceiling: 90_000);

        nonTaxable.Should().Be(90_000);
        taxable.Should().Be(0);
    }

    [Fact]
    public void GrossOverCeiling_SplitsIntoCeilingAndExcess()
    {
        var (nonTaxable, taxable) = PayrollProcessorService.ComputeThirteenthMonthTaxSplit(gross: 125_000, ceiling: 90_000);

        nonTaxable.Should().Be(90_000);
        taxable.Should().Be(35_000);
    }

    [Fact]
    public void ZeroGross_IsFullyNonTaxable_NoNegativeExcess()
    {
        var (nonTaxable, taxable) = PayrollProcessorService.ComputeThirteenthMonthTaxSplit(gross: 0, ceiling: 90_000);

        nonTaxable.Should().Be(0);
        taxable.Should().Be(0);
    }

    [Fact]
    public void CustomCeiling_IsRespected()
    {
        var (nonTaxable, taxable) = PayrollProcessorService.ComputeThirteenthMonthTaxSplit(gross: 100_000, ceiling: 50_000);

        nonTaxable.Should().Be(50_000);
        taxable.Should().Be(50_000);
    }
}

/// <summary>
/// PayrollProcessorService.ComputeRemainingThirteenthMonthCeiling — per Payroll Settings, the
/// exemption ceiling covers 13th month pay COMBINED with Special Bonuses already paid that
/// year, not 13th month pay alone. This computes how much ceiling room is left for the 13th
/// month payout after Special Bonuses already consumed some of it.
/// </summary>
public class ThirteenthMonthRemainingCeilingTests
{
    [Fact]
    public void NoPriorBonuses_FullCeilingRemains()
    {
        PayrollProcessorService.ComputeRemainingThirteenthMonthCeiling(ceiling: 90_000, priorSpecialBonuses: 0)
            .Should().Be(90_000);
    }

    [Fact]
    public void BonusesUnderCeiling_ReducesRemainingCeilingByThatAmount()
    {
        PayrollProcessorService.ComputeRemainingThirteenthMonthCeiling(ceiling: 90_000, priorSpecialBonuses: 80_000)
            .Should().Be(10_000);
    }

    [Fact]
    public void BonusesExactlyAtCeiling_NoRemainingCeiling()
    {
        PayrollProcessorService.ComputeRemainingThirteenthMonthCeiling(ceiling: 90_000, priorSpecialBonuses: 90_000)
            .Should().Be(0);
    }

    [Fact]
    public void BonusesExceedCeiling_RemainingCeilingClampsToZero_NotNegative()
    {
        PayrollProcessorService.ComputeRemainingThirteenthMonthCeiling(ceiling: 90_000, priorSpecialBonuses: 120_000)
            .Should().Be(0);
    }
}
