namespace hrms.test.StatutoryTests.WTax;

/// <summary>
/// AnnualTaxCalculator.GetAnnualTaxDue — the annual bracket lookup behind Year-End Tax
/// Annualization (TaxAnnualizationService). Unlike WTaxHelper.GetTable, AnnualTaxTable has no
/// PayrollType/frequency discriminator (a single annual bracket set), so there's no frequency
/// filter to get wrong — just RangeFrom/RangeTo matching and the
/// BaseTaxDue + (excess * AddOnPercentage) formula shared with the monthly table.
/// </summary>
public class AnnualTaxCalculatorTests
{
    // TRAIN law brackets, same shape as seeded by AccountInitService.
    private static readonly List<AnnualTaxTable> Brackets = new()
    {
        new AnnualTaxTable { RangeFrom = 0, RangeTo = 250_000, BaseTaxDue = 0, AddOnPercentage = 0m },
        new AnnualTaxTable { RangeFrom = 250_000, RangeTo = 400_000, BaseTaxDue = 0, AddOnPercentage = 0.15m },
        new AnnualTaxTable { RangeFrom = 400_000, RangeTo = 800_000, BaseTaxDue = 22_500, AddOnPercentage = 0.20m },
        new AnnualTaxTable { RangeFrom = 800_000, RangeTo = 2_000_000, BaseTaxDue = 102_500, AddOnPercentage = 0.25m },
        new AnnualTaxTable { RangeFrom = 2_000_000, RangeTo = 8_000_000, BaseTaxDue = 402_500, AddOnPercentage = 0.30m },
        new AnnualTaxTable { RangeFrom = 8_000_000, RangeTo = 999_999_999, BaseTaxDue = 2_202_500, AddOnPercentage = 0.35m },
    };

    [Fact]
    public void ZeroBracket_IncomeAtOrBelow250k_OwesNothing()
    {
        AnnualTaxCalculator.GetAnnualTaxDue(Brackets, 250_000).Should().Be(0);
    }

    [Fact]
    public void SecondBracket_ComputesBasePlusExcessOverRangeFrom()
    {
        // 300,000 falls in the 250k-400k @15% bracket: 0 + (300,000-250,000)*15% = 7,500
        AnnualTaxCalculator.GetAnnualTaxDue(Brackets, 300_000).Should().Be(7_500);
    }

    [Fact]
    public void HigherBracket_UsesItsOwnBaseTaxDue_NotZero()
    {
        // 900,000 falls in the 800k-2M @25% bracket: 102,500 + (900,000-800,000)*25% = 127,500
        AnnualTaxCalculator.GetAnnualTaxDue(Brackets, 900_000).Should().Be(127_500);
    }

    [Fact]
    public void NoMatchingBracket_ReturnsZero_NotAnException()
    {
        AnnualTaxCalculator.GetAnnualTaxDue(new List<AnnualTaxTable>(), 500_000).Should().Be(0);
    }

    [Fact]
    public void IncomeAboveEveryConfiguredBracket_FallsBackToTheHighestBracket_NotZero()
    {
        // A misconfigured/edited table (a gap, or a top bracket edited to a lower RangeTo than
        // reality) must never silently produce zero tax for a high earner — the engine falls
        // back to the bracket with the highest RangeTo and taxes the full excess at its rate,
        // per the spec's own "fallback to the final open-ended bracket" requirement.
        var brackets = new List<AnnualTaxTable>
        {
            new AnnualTaxTable { RangeFrom = 0, RangeTo = 250_000, BaseTaxDue = 0, AddOnPercentage = 0m },
            new AnnualTaxTable { RangeFrom = 250_000.01m, RangeTo = 400_000, BaseTaxDue = 0, AddOnPercentage = 0.15m },
        };
        // 1,000,000 is above every bracket's RangeTo (max configured is 400,000) — must fall
        // back to that 250k-400k@15% bracket rather than returning 0.
        var due = AnnualTaxCalculator.GetAnnualTaxDue(brackets, 1_000_000);
        due.Should().Be(0 + (1_000_000 - 250_000.01m) * 0.15m);
        due.Should().NotBe(0);
    }

    [Fact]
    public void NegativeTaxableIncome_ClampsExcessToZero_ReturnsJustTheBracketBase()
    {
        // A refund-heavy employee could in principle have a computed AnnualTaxableIncome below
        // a bracket's own RangeFrom if upstream inputs are unusual — Math.Max(0, ...) must
        // prevent a negative "excess" from reducing BaseTaxDue below the bracket's own floor.
        var brackets = new List<AnnualTaxTable>
        {
            new AnnualTaxTable { RangeFrom = 100_000, RangeTo = 200_000, BaseTaxDue = 5_000, AddOnPercentage = 0.10m },
        };
        AnnualTaxCalculator.GetAnnualTaxDue(brackets, 100_000).Should().Be(5_000);
    }
}
