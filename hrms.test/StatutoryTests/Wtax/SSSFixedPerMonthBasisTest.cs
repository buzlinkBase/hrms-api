using Hrms.Domain;
using Xunit;

namespace hrms.test;

public class TaxInfoFixedPerMonthBasisTest
{
    [Fact]
    public void ShouldComputeTaxInfoContribution_ForFixedBasisMonthlyVariableSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(100, result.TaxInfo.TaxDue);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeTaxInfoContribution_ForFixedBasisMonthlyVariableSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        WTaxTestHelpers.SetCutoff(context, 15);
        WTaxTestHelpers.SetCutoff(context, 31, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(50, result.TaxInfo.TaxDue);
        Assert.Equal(14_950m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeTaxInfoContribution_ForFixedBasisMonthlyVariableSalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 11, 01);
        var toDate = new DateOnly(2025, 11, 01);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(20m, result.TaxInfo.TaxDue);
        Assert.Equal(14980m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeTaxInfoContribution_ForFixedBasisMonthlyVariableSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 01);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(3.2258064516129032258064516129M, result.TaxInfo.TaxDue);
        Assert.Equal(14996.774193548387096774193548m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// MONTHLY_FIXED
    /// </summary>
    [Fact]
    public void ShouldComputeTaxInfoContribution_ForFixedBasisMonthlyFixedSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(100, result.TaxInfo.TaxDue);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeTaxInfoContribution_ForFixedBasisMonthlyFixedSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        WTaxTestHelpers.SetCutoff(context, 15);
        WTaxTestHelpers.SetCutoff(context, 31, true);

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(50, result.TaxInfo.TaxDue);
        Assert.Equal(14_950m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }

    [Fact]
    public void ShouldComputeTaxInfoContribution_ForFixedBasisMonthlyFixedSalaryWeeklyPayroll()
    {
        //cross month without prior contribution
        var fromDate = new DateOnly(2025, 11, 26);
        var toDate = new DateOnly(2025, 12, 10);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(100, result.TaxInfo.TaxDue);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeTaxInfoContribution_ForFixedBasisMonthlyFixedSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 01);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(3.2258064516129032258064516129M, result.TaxInfo.TaxDue);
        Assert.Equal(14996.774193548387096774193548m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// DAILY
    /// </summary>
    [Fact]
    public void ShouldComputeTaxInfoContribution_ForFixedBasisDailySalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(100, result.TaxInfo.TaxDue);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }


    [Fact]
    public void ShouldComputeTaxInfoContribution_ForFixedBasisDailySalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        WTaxTestHelpers.SetCutoff(context, 15);
        WTaxTestHelpers.SetCutoff(context, 31, true);

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(50, result.TaxInfo.TaxDue);
        Assert.Equal(14_950m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeTaxInfoContribution_ForFixedBasisDailySalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 22);
        var toDate = new DateOnly(2025, 12, 26);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);
        Assert.NotNull(result.TaxInfo);
        Assert.Equal(20m, result.TaxInfo.TaxDue);
        Assert.Equal(14980m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeTaxInfoContribution_ForFixedBasisDailySalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 31);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m, // divisor 26 assumed
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(100, result.TaxInfo.TaxDue);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }
}
