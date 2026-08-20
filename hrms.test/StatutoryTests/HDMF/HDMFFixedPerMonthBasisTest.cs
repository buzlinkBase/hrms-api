using Hrms.Domain;
using Xunit;

namespace hrms.test;

public class HDMFFixedPerMonthBasisTest
{
    [Fact]
    public void ShouldComputeHDMFContribution_ForFixedBasisMonthlyVariableSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
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

        Assert.NotNull(result.HDMF);
        Assert.Equal(100, result.HDMF.EE);
        Assert.Equal(200, result.HDMF.ER);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeHDMFContribution_ForFixedBasisMonthlyVariableSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        HDMFTestHelpers.SetCutoff(context, 1);
        HDMFTestHelpers.SetCutoff(context, 15);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(50, result.HDMF.EE);
        Assert.Equal(100, result.HDMF.ER);
        Assert.Equal(14_950m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForFixedBasisMonthlyVariableSalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 06);
        var context = HDMFTestHelpers.BuildContext(
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

        Assert.NotNull(result.HDMF);
        Assert.Equal(20m, result.HDMF.EE);
        Assert.Equal(40, result.HDMF.ER);
        Assert.Equal(14980m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeHDMFContribution_ForFixedBasisMonthlyVariableSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 01);
        var context = HDMFTestHelpers.BuildContext(
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

        Assert.NotNull(result.HDMF);
        Assert.Equal(3.2258064516129032258064516129M, result.HDMF.EE);
        Assert.Equal(6.4516129032258064516129032258M, result.HDMF.ER);
        Assert.Equal(14996.774193548387096774193548m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// MONTHLY_FIXED
    /// </summary>
    [Fact]
    public void ShouldComputeHDMFContribution_ForFixedBasisMonthlyFixedSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
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

        Assert.NotNull(result.HDMF);
        Assert.Equal(100, result.HDMF.EE);
        Assert.Equal(200, result.HDMF.ER);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForFixedBasisMonthlyFixedSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        HDMFTestHelpers.SetCutoff(context, 1);
        HDMFTestHelpers.SetCutoff(context, 15);

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(50, result.HDMF.EE);
        Assert.Equal(100, result.HDMF.ER);
        Assert.Equal(14_950m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForFixedBasisMonthlyFixedSalaryWeeklyPayroll()
    {
        //cross month without prior contribution
        var fromDate = new DateOnly(2025, 11, 26);
        var toDate = new DateOnly(2025, 12, 10);
        var context = HDMFTestHelpers.BuildContext(
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

        Assert.NotNull(result.HDMF);
        Assert.Equal(100, result.HDMF.EE);
        Assert.Equal(200, result.HDMF.ER);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeHDMFContribution_ForFixedBasisMonthlyFixedSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 01);
        var context = HDMFTestHelpers.BuildContext(
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

        Assert.NotNull(result.HDMF);
        Assert.Equal(3.23M, Math.Round(result.HDMF.EE, 2));
        Assert.Equal(6.45M, Math.Round(result.HDMF.ER, 2));
        Assert.Equal(14996.77m, Math.Round(result.RemainingGrossBalance, 2));
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// DAILY
    /// </summary>
    [Fact]
    public void ShouldComputeHDMFContribution_ForFixedBasisDailySalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
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

        Assert.NotNull(result.HDMF);
        Assert.Equal(100, result.HDMF.EE);
        Assert.Equal(200, result.HDMF.ER);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }


    [Fact]
    public void ShouldComputeHDMFContribution_ForFixedBasisDailySalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        HDMFTestHelpers.SetCutoff(context, 1);
        HDMFTestHelpers.SetCutoff(context, 15);

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(50, result.HDMF.EE);
        Assert.Equal(100, result.HDMF.ER);
        Assert.Equal(14_950m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForFixedBasisDailySalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 22);
        var toDate = new DateOnly(2025, 12, 26);
        var context = HDMFTestHelpers.BuildContext(
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
        Assert.NotNull(result.HDMF);
        Assert.Equal(20m, result.HDMF.EE);
        Assert.Equal(40, result.HDMF.ER);
        Assert.Equal(14980m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForFixedBasisDailySalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 31);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(100, result.HDMF.EE);
        Assert.Equal(200, result.HDMF.ER);
        Assert.Equal(300, result.HDMF.Total);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }
}
