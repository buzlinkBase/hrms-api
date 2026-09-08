using Hrms.Domain.Entities;

namespace Hrms.Core.Policies.DeductionPolicies;

// Annual bracket lookup for Year-End Tax Annualization — the direct annual analog of
// WTaxHelper.GetTable/GetCalculatedDue, but against AnnualTaxTable, which (unlike TaxTable) has
// no PayrollType/frequency discriminator: it's a single annual bracket set, so no frequency
// filter is needed before matching RangeFrom/RangeTo. See TaxAnnualizationService.
internal static class AnnualTaxCalculator
{
    public static AnnualTaxTable? GetBracket(List<AnnualTaxTable> brackets, decimal annualTaxableIncome)
    {
        return brackets.FirstOrDefault(x => x.RangeFrom <= annualTaxableIncome && x.RangeTo >= annualTaxableIncome);
    }

    public static decimal GetAnnualTaxDue(List<AnnualTaxTable> brackets, decimal annualTaxableIncome)
    {
        if (brackets.Count == 0) return 0;
        // Fall back to the bracket with the highest RangeTo whenever annualTaxableIncome falls
        // above every configured bracket's own ceiling — otherwise a misconfigured/edited
        // table (a gap, or a top bracket edited to a lower RangeTo than reality) would silently
        // produce zero tax due for a high earner instead of taxing them at the top bracket's
        // own rate, which is what BIR annualization requires regardless of table gaps.
        var bracket = GetBracket(brackets, annualTaxableIncome)
            ?? brackets.OrderByDescending(x => x.RangeTo).First();
        return bracket.BaseTaxDue + (Math.Max(0, annualTaxableIncome - bracket.RangeFrom) * bracket.AddOnPercentage);
    }
}
