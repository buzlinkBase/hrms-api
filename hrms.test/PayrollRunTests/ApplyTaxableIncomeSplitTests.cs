namespace hrms.test.PayrollRunTests;

/// <summary>
/// EmployeePayrollLineService.ApplyTaxableIncomeSplit — populates the previously-dead
/// PayrollSummaryLine.TaxableIncome/NonTaxableIncome columns (see Payroll.cs's doc comment)
/// using the same formula already vetted for Year-End Tax Annualization
/// (TaxAnnualizationService.ComputeAsync) and PayrollOpeningBalance.DerivedTaxableIncome, so BIR
/// Alphalist/2316 (PayrollReportService.GetAlphalistAsync/Get2316DataAsync, which read these
/// columns directly) finally report real, aligned figures instead of always-zero ones.
/// </summary>
public class ApplyTaxableIncomeSplitTests
{
    [Fact]
    public void RegularShapedLine_NetsOutNonTaxableBenefitsAndStatutoryContributions()
    {
        var line = new PayrollSummaryLine
        {
            GrossIncome = 30_000,
            NonTaxableBenefits = 2_000,
            SSSContribution = 1_350,
            PhilHealthContribution = 500,
            PagIbigContribution = 200,
        };
        EmployeePayrollLineService.ApplyTaxableIncomeSplit(line);

        line.NonTaxableIncome.Should().Be(2_000 + 1_350 + 500 + 200);
        line.TaxableIncome.Should().Be(30_000 - (2_000 + 1_350 + 500 + 200));
    }

    [Fact]
    public void ThirteenthMonthOrLastPayShapedLine_ReducesToTaxableBenefitsSplit()
    {
        // ThirteenthMonth/LastPay lines never set SSS/PhilHealth/PagIbig (confirmed — see
        // ThirteenthMonthPayrollService.cs/LastPayrollService.cs), so the shared formula must
        // reduce to exactly the NonTaxableBenefits/TaxableBenefits split already computed by
        // ThirteenthMonthCeilingCalculator, with no statutory netting applied.
        var line = new PayrollSummaryLine
        {
            GrossIncome = 25_000,
            NonTaxableBenefits = 15_000,
            TaxableBenefits = 10_000,
        };
        EmployeePayrollLineService.ApplyTaxableIncomeSplit(line);

        line.NonTaxableIncome.Should().Be(15_000);
        line.TaxableIncome.Should().Be(10_000);
        line.TaxableIncome.Should().Be(line.TaxableBenefits);
    }

    [Fact]
    public void YearEndAdjustmentShapedLine_StaysZero()
    {
        // TaxAnnualizationService builds YearEndAdjustment lines with GrossIncome = 0 and never
        // touches NonTaxableBenefits — confirming the formula naturally contributes nothing to
        // Alphalist/2316's sums for these rows, matching today's behavior.
        var line = new PayrollSummaryLine { GrossIncome = 0 };
        EmployeePayrollLineService.ApplyTaxableIncomeSplit(line);

        line.NonTaxableIncome.Should().Be(0);
        line.TaxableIncome.Should().Be(0);
    }

    [Fact]
    public void NonTaxableAmountExceedingGross_ClampsTaxableIncomeToZero_NotNegative()
    {
        var line = new PayrollSummaryLine
        {
            GrossIncome = 5_000,
            NonTaxableBenefits = 6_000,
        };
        EmployeePayrollLineService.ApplyTaxableIncomeSplit(line);

        line.TaxableIncome.Should().Be(0);
        line.NonTaxableIncome.Should().Be(6_000);
    }
}
