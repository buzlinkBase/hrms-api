using Hrms.Domain.Entities.EmployeeEntities;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// PayrollOpeningBalanceService.ApplyComputedTotals — GrossIncome/TotalDeductions/NetPay are
/// never trusted from the client, always recomputed server-side from the entered components
/// (see PayrollOpeningBalance's doc comment) so every YTD report that folds one of these rows
/// in (GetYtdSummaryAsync/GetThirteenthMonthAsync/GetAnnualTaxAnnualizationInputsAsync/
/// GetAlphalistAsync/Get2316DataAsync) can trust a plain field-add.
/// </summary>
public class PayrollOpeningBalanceServiceTests
{
    [Fact]
    public void GrossIncome_SumsAllSixEarningsComponents()
    {
        var model = new PayrollOpeningBalance
        {
            BasicPay = 100_000,
            OvertimePay = 5_000,
            HolidayPay = 2_000,
            Allowances = 3_000,
            OtherIncome = 1_000,
            Bonuses = 10_000,
        };
        PayrollOpeningBalanceService.ApplyComputedTotals(model);
        model.GrossIncome.Should().Be(121_000);
    }

    [Fact]
    public void TotalDeductions_SumsAllFiveDeductionComponents()
    {
        var model = new PayrollOpeningBalance
        {
            SSSContribution = 1_000,
            PhilHealthContribution = 500,
            PagIbigContribution = 200,
            WithholdingTax = 8_000,
            OtherDeductions = 300,
        };
        PayrollOpeningBalanceService.ApplyComputedTotals(model);
        model.TotalDeductions.Should().Be(10_000);
    }

    [Fact]
    public void NetPay_IsGrossIncomeMinusTotalDeductions()
    {
        var model = new PayrollOpeningBalance
        {
            BasicPay = 100_000,
            SSSContribution = 1_000,
            PhilHealthContribution = 500,
            PagIbigContribution = 200,
            WithholdingTax = 8_000,
        };
        PayrollOpeningBalanceService.ApplyComputedTotals(model);
        model.NetPay.Should().Be(model.GrossIncome - model.TotalDeductions);
        model.NetPay.Should().Be(90_300);
    }

    [Fact]
    public void ExistingComputedFieldValues_AreOverwritten_NeverTrustedFromClient()
    {
        // Simulates an Update where the client-supplied payload never carries
        // GrossIncome/TotalDeductions/NetPay (CreatePayrollOpeningBalance/
        // UpdatePayrollOpeningBalance don't expose them) but the existing tracked entity still
        // has stale values from before the edit — those must be fully replaced, not merged.
        var model = new PayrollOpeningBalance
        {
            BasicPay = 50_000,
            GrossIncome = 999_999,
            TotalDeductions = 999_999,
            NetPay = 999_999,
        };
        PayrollOpeningBalanceService.ApplyComputedTotals(model);
        model.GrossIncome.Should().Be(50_000);
        model.TotalDeductions.Should().Be(0);
        model.NetPay.Should().Be(50_000);
    }

    [Fact]
    public void DerivedTaxableIncome_IsGrossLessNonTaxableAndStatutory()
    {
        // Used by GetAlphalistAsync/Get2316DataAsync — mirrors how Payroll's own
        // TaxableIncome column is computed upstream, since PayrollOpeningBalance doesn't
        // store TaxableIncome directly (same precedent as PriorEmployerTaxRecord).
        var model = new PayrollOpeningBalance
        {
            BasicPay = 300_000,
            NonTaxableIncome = 20_000,
            SSSContribution = 10_000,
            PhilHealthContribution = 5_000,
            PagIbigContribution = 1_200,
        };
        PayrollOpeningBalanceService.ApplyComputedTotals(model);
        model.DerivedTaxableIncome.Should().Be(300_000 - 20_000 - 10_000 - 5_000 - 1_200);
    }
}
