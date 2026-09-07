using Hrms.Core.Services;
using Hrms.Domain.Entities;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// PayrollReportService.SummarizeMonthlyRemittanceReturn — the pure aggregation step behind
/// the BIR 1601-C report. Made internal static (not private) specifically so this can be
/// tested here without a database — see Hrms.Core's InternalsVisibleTo for hrms.test.
/// Covers spec Validations 1/2 (Line 17 = 16A+16B+16C, Line 18 = Line15-Line17), which hold by
/// construction here, and Validation 3 (unwithheld-tax warning).
/// </summary>
public class MonthlyRemittanceReturnSummaryTests
{
    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 1, 31);

    private static MonthlyRemittanceReturnEmployeeModel MweEmployee(
        decimal gross, decimal minWage, decimal premiumPay, decimal otherNonTaxable, decimal taxWithheld) => new()
    {
        EmployeeId = Guid.NewGuid(),
        IsMinimumWageEarner = true,
        AtcCode = "KR020",
        GrossCompensation = gross,
        StatutoryMinimumWage = minWage,
        MWEPremiumPay = premiumPay,
        OtherNonTaxable = otherNonTaxable,
        TaxableCompensation = gross - (minWage + premiumPay + otherNonTaxable),
        TaxWithheld = taxWithheld,
    };

    private static MonthlyRemittanceReturnEmployeeModel RegularEmployee(
        decimal gross, decimal otherNonTaxable, decimal taxWithheld) => new()
    {
        EmployeeId = Guid.NewGuid(),
        IsMinimumWageEarner = false,
        AtcCode = "KR010",
        GrossCompensation = gross,
        StatutoryMinimumWage = 0,
        MWEPremiumPay = 0,
        OtherNonTaxable = otherNonTaxable,
        TaxableCompensation = gross - otherNonTaxable,
        TaxWithheld = taxWithheld,
    };

    [Fact]
    public void NoEmployees_AllLinesZero_NoWarning()
    {
        var summary = PayrollReportService.SummarizeMonthlyRemittanceReturn(new(), From, To, amendedReturn: false);

        summary.EmployeeCount.Should().Be(0);
        summary.Line15_TotalCompensation.Should().Be(0);
        summary.Line17_TotalNonTaxable.Should().Be(0);
        summary.Line18_TaxableCompensation.Should().Be(0);
        summary.HasUnwithheldTaxWarning.Should().BeFalse();
    }

    [Fact]
    public void MixOfMweAndRegularEmployees_LinesSumCorrectly_Line17And18HoldByConstruction()
    {
        var employees = new List<MonthlyRemittanceReturnEmployeeModel>
        {
            MweEmployee(gross: 15_000, minWage: 12_000, premiumPay: 1_500, otherNonTaxable: 1_000, taxWithheld: 0),
            RegularEmployee(gross: 40_000, otherNonTaxable: 3_000, taxWithheld: 2_500),
        };

        var summary = PayrollReportService.SummarizeMonthlyRemittanceReturn(employees, From, To, amendedReturn: false);

        summary.EmployeeCount.Should().Be(2);
        summary.Line15_TotalCompensation.Should().Be(55_000); // 15,000 + 40,000
        summary.Line16A_StatutoryMinimumWage.Should().Be(12_000);
        summary.Line16B_MWEPremiumPay.Should().Be(1_500);
        summary.Line16C_OtherNonTaxable.Should().Be(4_000); // 1,000 + 3,000

        // Validation 1 (spec §6): Line 17 == 16A + 16B + 16C
        summary.Line17_TotalNonTaxable.Should().Be(
            summary.Line16A_StatutoryMinimumWage + summary.Line16B_MWEPremiumPay + summary.Line16C_OtherNonTaxable);
        summary.Line17_TotalNonTaxable.Should().Be(17_500);

        // Validation 2 (spec §6): Line 18 == Line 15 - Line 17
        summary.Line18_TaxableCompensation.Should().Be(summary.Line15_TotalCompensation - summary.Line17_TotalNonTaxable);
        summary.Line18_TaxableCompensation.Should().Be(37_500);

        summary.Line19_TaxWithheld.Should().Be(2_500);
    }

    [Fact]
    public void AmendedReturnFlag_IsPassedThroughUnchanged()
    {
        var summary = PayrollReportService.SummarizeMonthlyRemittanceReturn(new(), From, To, amendedReturn: true);
        summary.AmendedReturn.Should().BeTrue();

        var notAmended = PayrollReportService.SummarizeMonthlyRemittanceReturn(new(), From, To, amendedReturn: false);
        notAmended.AmendedReturn.Should().BeFalse();
    }

    [Fact]
    public void TaxableCompensationPositive_ButNoTaxWithheld_TriggersWarning()
    {
        var employees = new List<MonthlyRemittanceReturnEmployeeModel>
        {
            RegularEmployee(gross: 30_000, otherNonTaxable: 2_000, taxWithheld: 0),
        };

        var summary = PayrollReportService.SummarizeMonthlyRemittanceReturn(employees, From, To, amendedReturn: false);

        summary.Line18_TaxableCompensation.Should().BeGreaterThan(0);
        summary.Line19_TaxWithheld.Should().Be(0);
        summary.HasUnwithheldTaxWarning.Should().BeTrue();
    }

    [Fact]
    public void TaxableCompensationZeroOrNegative_NoWarningEvenWithoutWithholding()
    {
        // All-MWE period where non-taxable lines fully absorb gross compensation — legitimately
        // zero withholding, not a data-quality problem.
        var employees = new List<MonthlyRemittanceReturnEmployeeModel>
        {
            MweEmployee(gross: 12_000, minWage: 12_000, premiumPay: 0, otherNonTaxable: 0, taxWithheld: 0),
        };

        var summary = PayrollReportService.SummarizeMonthlyRemittanceReturn(employees, From, To, amendedReturn: false);

        summary.Line18_TaxableCompensation.Should().Be(0);
        summary.HasUnwithheldTaxWarning.Should().BeFalse();
    }

    [Fact]
    public void UnclassifiedEmployees_AreCountedInSummary()
    {
        var classified = RegularEmployee(gross: 30_000, otherNonTaxable: 2_000, taxWithheld: 2_000);
        var unclassified1 = RegularEmployee(gross: 15_000, otherNonTaxable: 0, taxWithheld: 0);
        unclassified1.IsUnclassified = true;
        var unclassified2 = RegularEmployee(gross: 16_000, otherNonTaxable: 0, taxWithheld: 0);
        unclassified2.IsUnclassified = true;

        var summary = PayrollReportService.SummarizeMonthlyRemittanceReturn(
            new List<MonthlyRemittanceReturnEmployeeModel> { classified, unclassified1, unclassified2 },
            From, To, amendedReturn: false);

        summary.UnclassifiedEmployeeCount.Should().Be(2);
        summary.EmployeeCount.Should().Be(3);
    }

    [Fact]
    public void NoUnclassifiedEmployees_CountIsZero()
    {
        var employees = new List<MonthlyRemittanceReturnEmployeeModel>
        {
            RegularEmployee(gross: 30_000, otherNonTaxable: 2_000, taxWithheld: 2_000),
            MweEmployee(gross: 12_000, minWage: 12_000, premiumPay: 0, otherNonTaxable: 0, taxWithheld: 0),
        };

        var summary = PayrollReportService.SummarizeMonthlyRemittanceReturn(employees, From, To, amendedReturn: false);

        summary.UnclassifiedEmployeeCount.Should().Be(0);
    }
}

/// <summary>
/// PayrollReportService.ResolveRegionRate — the region+class minimum-wage-rate lookup behind
/// BIR 1601-C's Minimum-Wage-Earner classification. Pure over an already-fetched rate list, so
/// testable without a database — same reasoning as MonthlyRemittanceReturnSummaryTests above.
/// </summary>
public class ResolveRegionRateTests
{
    private static readonly DateOnly AsOf = new(2026, 1, 15);

    private static MinimumWageRate Rate(string region, decimal dailyRate, string? wageOrderClass = null, DateOnly? effectiveDate = null) => new()
    {
        RegionCode = region,
        RegionName = region,
        DailyRate = dailyRate,
        EffectiveDate = effectiveDate ?? new DateOnly(2025, 1, 1),
        WageOrderClass = wageOrderClass,
    };

    [Fact]
    public void RegionWithOnlyClasslessRate_BranchHasNoClass_ResolvesToThatRate()
    {
        var rates = new List<MinimumWageRate> { Rate("NCR", 610) };

        var result = PayrollReportService.ResolveRegionRate(rates, "NCR", null, AsOf);

        result.Should().Be(610);
    }

    [Fact]
    public void RegionWithTwoClasses_BranchAssignedToClassA_MatchesClassAOnly()
    {
        var rates = new List<MinimumWageRate>
        {
            Rate("NCR", 610, wageOrderClass: "Non-Agriculture"),
            Rate("NCR", 573, wageOrderClass: "Retail/Service <=10 workers"),
        };

        var result = PayrollReportService.ResolveRegionRate(rates, "NCR", "Non-Agriculture", AsOf);

        result.Should().Be(610);
    }

    [Fact]
    public void BranchClassHasNoMatchingRate_FallsBackToClasslessRegionRate()
    {
        var rates = new List<MinimumWageRate>
        {
            Rate("NCR", 610), // general/class-less rate
        };

        var result = PayrollReportService.ResolveRegionRate(rates, "NCR", "Agriculture-Plantation", AsOf);

        result.Should().Be(610);
    }

    [Fact]
    public void BranchClassHasNoMatch_AndNoClasslessRateExists_IsUnclassified()
    {
        var rates = new List<MinimumWageRate>
        {
            Rate("NCR", 610, wageOrderClass: "Non-Agriculture"),
        };

        var result = PayrollReportService.ResolveRegionRate(rates, "NCR", "Agriculture-Plantation", AsOf);

        result.Should().BeNull();
    }

    [Fact]
    public void NoRegionCode_ReturnsNull()
    {
        var rates = new List<MinimumWageRate> { Rate("NCR", 610) };

        var result = PayrollReportService.ResolveRegionRate(rates, null, null, AsOf);

        result.Should().BeNull();
    }

    [Fact]
    public void MostRecentEffectiveDateWins_WithinSameClass()
    {
        var rates = new List<MinimumWageRate>
        {
            Rate("NCR", 570, effectiveDate: new DateOnly(2024, 1, 1)),
            Rate("NCR", 610, effectiveDate: new DateOnly(2025, 6, 1)),
        };

        var result = PayrollReportService.ResolveRegionRate(rates, "NCR", null, AsOf);

        result.Should().Be(610);
    }

    [Fact]
    public void RateEffectiveAfterAsOfDate_IsIgnored()
    {
        var rates = new List<MinimumWageRate>
        {
            Rate("NCR", 610, effectiveDate: new DateOnly(2025, 1, 1)),
            Rate("NCR", 650, effectiveDate: new DateOnly(2026, 6, 1)), // future relative to AsOf
        };

        var result = PayrollReportService.ResolveRegionRate(rates, "NCR", null, AsOf);

        result.Should().Be(610);
    }
}
