using Hrms.Domain;
using Xunit;

namespace hrms.test;

public class SSSNoneComputeBasisTest
{

    /// <summary>
    /// MONTHLY_VARIABLE
    /// </summary>
    [Fact]
    public void ShouldComputeSSSContribution_ForNoneBasisMonthlyVariableSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(0, result.SSS.EE);
        Assert.Equal(0, result.SSS.ER);
        Assert.Equal(0, result.SSS.EC);
        Assert.Equal(0, result.SSS.TotalER);
        Assert.Equal(0, result.SSS.Total);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeSSSContribution_ForNoneBasisMonthlyVariableSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(0, result.SSS.EE);
        Assert.Equal(0, result.SSS.ER);
        Assert.Equal(0, result.SSS.EC);
        Assert.Equal(0, result.SSS.TotalER);
        Assert.Equal(0, result.SSS.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeSSSContribution_ForNoneBasisMonthlyVariableSalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 11, 26);
        var toDate = new DateOnly(2025, 12, 10);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(0, result.SSS.EE);
        Assert.Equal(0, result.SSS.ER);
        Assert.Equal(0, result.SSS.EC);
        Assert.Equal(0, result.SSS.TotalER);
        Assert.Equal(0, result.SSS.Total);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeSSSContribution_ForNoneBasisMonthlyVariableSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(0, result.SSS.EE);
        Assert.Equal(0, result.SSS.ER);
        Assert.Equal(0, result.SSS.EC);
        Assert.Equal(0, result.SSS.TotalER);
        Assert.Equal(0, result.SSS.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// MONTHLY_FIXED
    /// </summary>
    [Fact]
    public void ShouldComputeSSSContribution_ForNoneBasisMonthlyFixedSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(0, result.SSS.EE);
        Assert.Equal(0, result.SSS.ER);
        Assert.Equal(0, result.SSS.EC);
        Assert.Equal(0, result.SSS.TotalER);
        Assert.Equal(0, result.SSS.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeSSSContribution_ForNoneBasisMonthlyFixedSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(0, result.SSS.EE);
        Assert.Equal(0, result.SSS.ER);
        Assert.Equal(0, result.SSS.EC);
        Assert.Equal(0, result.SSS.TotalER);
        Assert.Equal(0, result.SSS.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeSSSContribution_ForNoneBasisMonthlyFixedSalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 11, 26);
        var toDate = new DateOnly(2025, 12, 10);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(0, result.SSS.EE);
        Assert.Equal(0, result.SSS.ER);
        Assert.Equal(0, result.SSS.EC);
        Assert.Equal(0, result.SSS.TotalER);
        Assert.Equal(0, result.SSS.Total);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeSSSContribution_ForNoneBasisMonthlyFixedSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(0, result.SSS.EE);
        Assert.Equal(0, result.SSS.ER);
        Assert.Equal(0, result.SSS.EC);
        Assert.Equal(0, result.SSS.TotalER);
        Assert.Equal(0, result.SSS.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// DAILY
    /// </summary>
    [Fact]
    public void ShouldComputeSSSContribution_ForNoneBasisDailySalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(0, result.SSS.EE);
        Assert.Equal(0, result.SSS.ER);
        Assert.Equal(0, result.SSS.EC);
        Assert.Equal(0, result.SSS.TotalER);
        Assert.Equal(0, result.SSS.Total);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeSSSContribution_ForNoneBasisDailySalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(0, result.SSS.EE);
        Assert.Equal(0, result.SSS.ER);
        Assert.Equal(0, result.SSS.EC);
        Assert.Equal(0, result.SSS.TotalER);
        Assert.Equal(0, result.SSS.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeSSSContribution_ForNoneBasisDailySalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 11, 26);
        var toDate = new DateOnly(2025, 12, 10);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(0, result.SSS.EE);
        Assert.Equal(0, result.SSS.ER);
        Assert.Equal(0, result.SSS.EC);
        Assert.Equal(0, result.SSS.TotalER);
        Assert.Equal(0, result.SSS.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeSSSContribution_ForNoneBasisDailySalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(0, result.SSS.EE);
        Assert.Equal(0, result.SSS.ER);
        Assert.Equal(0, result.SSS.EC);
        Assert.Equal(0, result.SSS.TotalER);
        Assert.Equal(0, result.SSS.Total);
        Assert.Empty(result.ScheduledDeductions);
    }

}
