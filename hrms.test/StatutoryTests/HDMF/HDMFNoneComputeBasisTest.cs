using Hrms.Domain;
using Xunit;

namespace hrms.test;

public class HDMFNoneComputeBasisTest
{

    /// <summary>
    /// MONTHLY_VARIABLE
    /// </summary>
    [Fact]
    public void ShouldComputeHDMFContribution_ForNoneBasisMonthlyVariableSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.None,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(0, result.HDMF.EE);
        Assert.Equal(0, result.HDMF.ER);
        Assert.Equal(0, result.HDMF.Total);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeHDMFContribution_ForNoneBasisMonthlyVariableSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.None,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(0, result.HDMF.EE);
        Assert.Equal(0, result.HDMF.ER);
        Assert.Equal(0, result.HDMF.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForNoneBasisMonthlyVariableSalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 11, 26);
        var toDate = new DateOnly(2025, 12, 10);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.None,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(0, result.HDMF.EE);
        Assert.Equal(0, result.HDMF.ER);
        Assert.Equal(0, result.HDMF.Total);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeHDMFContribution_ForNoneBasisMonthlyVariableSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.None,
            monthlyRate: 10_000,
            dailyRate: 384.61m, // divisor 26 assumed
            grossPay: 15_000
        );


        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(0, result.HDMF.EE);
        Assert.Equal(0, result.HDMF.ER);
        Assert.Equal(0, result.HDMF.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// MONTHLY_FIXED
    /// </summary>
    [Fact]
    public void ShouldComputeHDMFContribution_ForNoneBasisMonthlyFixedSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.None,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(0, result.HDMF.EE);
        Assert.Equal(0, result.HDMF.ER);
        Assert.Equal(0, result.HDMF.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForNoneBasisMonthlyFixedSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.None,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(0, result.HDMF.EE);
        Assert.Equal(0, result.HDMF.ER);
        Assert.Equal(0, result.HDMF.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForNoneBasisMonthlyFixedSalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 11, 26);
        var toDate = new DateOnly(2025, 12, 10);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.None,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(0, result.HDMF.EE);
        Assert.Equal(0, result.HDMF.ER);
        Assert.Equal(0, result.HDMF.Total);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeHDMFContribution_ForNoneBasisMonthlyFixedSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.None,
            monthlyRate: 10_000,
            dailyRate: 384.61m, // divisor 26 assumed
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(0, result.HDMF.EE);
        Assert.Equal(0, result.HDMF.ER);
        Assert.Equal(0, result.HDMF.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// DAILY
    /// </summary>
    [Fact]
    public void ShouldComputeHDMFContribution_ForNoneBasisDailySalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.None,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(0, result.HDMF.EE);
        Assert.Equal(0, result.HDMF.ER);
        Assert.Equal(0, result.HDMF.Total);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeHDMFContribution_ForNoneBasisDailySalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.None,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(0, result.HDMF.EE);
        Assert.Equal(0, result.HDMF.ER);
        Assert.Equal(0, result.HDMF.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForNoneBasisDailySalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 11, 26);
        var toDate = new DateOnly(2025, 12, 10);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.None,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(0, result.HDMF.EE);
        Assert.Equal(0, result.HDMF.ER);
        Assert.Equal(0, result.HDMF.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForNoneBasisDailySalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.None,
            monthlyRate: 10_000,
            dailyRate: 384.61m, // divisor 26 assumed
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(0, result.HDMF.EE);
        Assert.Equal(0, result.HDMF.ER);
        Assert.Equal(0, result.HDMF.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

}
