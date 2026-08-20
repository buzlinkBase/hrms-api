using Hrms.Domain;
using Xunit;

namespace hrms.test;

public class HDMFFixedPerPayrollBasisTest
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
            computationBasis: ComputationBasis.FixedPerPayroll,
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
            computationBasis: ComputationBasis.FixedPerPayroll,
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
    public void ShouldComputeHDMFContribution_ForFixedBasisMonthlyVariableSalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 11, 01);
        var toDate = new DateOnly(2025, 11, 01);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.FixedPerPayroll,
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
    public void ShouldComputeHDMFContribution_ForFixedBasisMonthlyVariableSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 01);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.FixedPerPayroll,
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
            computationBasis: ComputationBasis.FixedPerPayroll,
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
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.FixedPerPayroll,
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
            computationBasis: ComputationBasis.FixedPerPayroll,
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
            computationBasis: ComputationBasis.FixedPerPayroll,
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
            computationBasis: ComputationBasis.FixedPerPayroll,
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
            computationBasis: ComputationBasis.FixedPerPayroll,
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
    public void ShouldComputeHDMFContribution_ForFixedBasisDailySalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 22);
        var toDate = new DateOnly(2025, 12, 26);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.FixedPerPayroll,
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
    public void ShouldComputeHDMFContribution_ForFixedBasisDailySalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 31);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.FixedPerPayroll,
            monthlyRate: 10_000,
            dailyRate: 384.61m, // divisor 26 assumed
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
}
