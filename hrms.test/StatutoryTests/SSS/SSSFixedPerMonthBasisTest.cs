using FluentAssertions;
using Hrms.Domain;
using Xunit;

namespace hrms.test;

public class SSSFixedPerMonthBasisTest
{
    [Fact]
    public void ShouldComputeSSSContribution_ForFixedBasisMonthlyVariableSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(100, result.SSS.EE);
        Assert.Equal(200, result.SSS.ER);
        Assert.Equal(10, result.SSS.EC);
        Assert.Equal(210, result.SSS.TotalER);
        Assert.Equal(310, result.SSS.Total);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeSSSContribution_ForFixedBasisMonthlyVariableSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        SSSTestHelpers.SetCutoff(context, 15);
        SSSTestHelpers.SetCutoff(context, 31, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(50, result.SSS.EE);
        Assert.Equal(100, result.SSS.ER);
        Assert.Equal(5, result.SSS.EC);
        Assert.Equal(105, result.SSS.TotalER);
        Assert.Equal(155, result.SSS.Total);
        Assert.Equal(14_950m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeSSSContribution_ForFixedBasisMonthlyVariableSalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 06);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(20m, result.SSS.EE);
        Assert.Equal(40, result.SSS.ER);
        Assert.Equal(2, result.SSS.EC);
        Assert.Equal(42, result.SSS.TotalER);
        Assert.Equal(62, result.SSS.Total);
        Assert.Equal(14980m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeSSSContribution_ForFixedBasisMonthlyVariableSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 01);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(3.2258064516129032258064516129M, result.SSS.EE);
        Assert.Equal(6.4516129032258064516129032258M, result.SSS.ER);
        Assert.Equal(0.3225806451612903225806451613M, result.SSS.EC);
        Assert.Equal(6.7741935483870967741935483871m, result.SSS.TotalER);
        Assert.Equal(10.000000000000000000000000000m, result.SSS.Total);
        Assert.Equal(14996.774193548387096774193548m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// MONTHLY_FIXED
    /// </summary>
    [Fact]
    public void ShouldComputeSSSContribution_ForFixedBasisMonthlyFixedSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(100, result.SSS.EE);
        Assert.Equal(200, result.SSS.ER);
        Assert.Equal(10, result.SSS.EC);
        Assert.Equal(210, result.SSS.TotalER);
        Assert.Equal(310, result.SSS.Total);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeSSSContribution_ForFixedBasisMonthlyFixedSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        SSSTestHelpers.SetCutoff(context, 15);
        SSSTestHelpers.SetCutoff(context, 31, true);

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(50, result.SSS.EE);
        Assert.Equal(100, result.SSS.ER);
        Assert.Equal(5, result.SSS.EC);
        Assert.Equal(105, result.SSS.TotalER);
        Assert.Equal(155, result.SSS.Total);
        Assert.Equal(14_950m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }

    [Fact]
    public void ShouldComputeSSSContribution_ForFixedBasisMonthlyFixedSalaryWeeklyPayroll()
    {
        //cross month without prior contribution
        var fromDate = new DateOnly(2025, 11, 26);
        var toDate = new DateOnly(2025, 12, 10);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(100, result.SSS.EE);
        Assert.Equal(200, result.SSS.ER);
        Assert.Equal(10, result.SSS.EC);
        Assert.Equal(210, result.SSS.TotalER);
        Assert.Equal(310, result.SSS.Total);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeSSSContribution_ForFixedBasisMonthlyFixedSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 01);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(3.2258064516129032258064516129M, result.SSS.EE);
        Assert.Equal(6.4516129032258064516129032258M, result.SSS.ER);
        Assert.Equal(0.3225806451612903225806451613M, result.SSS.EC);
        Assert.Equal(6.7741935483870967741935483871m, result.SSS.TotalER);
        Assert.Equal(10.000000000000000000000000000m, result.SSS.Total);
        Assert.Equal(14996.774193548387096774193548m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// DAILY
    /// </summary>
    [Fact]
    public void ShouldComputeSSSContribution_ForFixedBasisDailySalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(100, result.SSS.EE);
        Assert.Equal(200, result.SSS.ER);
        Assert.Equal(10, result.SSS.EC);
        Assert.Equal(210, result.SSS.TotalER);
        Assert.Equal(310, result.SSS.Total);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }


    [Fact]
    public void ShouldComputeSSSContribution_ForFixedBasisDailySalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        SSSTestHelpers.SetCutoff(context, 15);
        SSSTestHelpers.SetCutoff(context, 31, true);

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(50, result.SSS.EE);
        Assert.Equal(100, result.SSS.ER);
        Assert.Equal(5, result.SSS.EC);
        Assert.Equal(105, result.SSS.TotalER);
        Assert.Equal(155, result.SSS.Total);
        Assert.Equal(14_950m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeSSSContribution_ForFixedBasisDailySalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 22);
        var toDate = new DateOnly(2025, 12, 26);
        var context = SSSTestHelpers.BuildContext(
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
        Assert.NotNull(result.SSS);
        Assert.Equal(20m, result.SSS.EE);
        Assert.Equal(40, result.SSS.ER);
        Assert.Equal(2, result.SSS.EC);
        Assert.Equal(42, result.SSS.TotalER);
        Assert.Equal(62, result.SSS.Total);
        Assert.Equal(14980m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeSSSContribution_ForFixedBasisDailySalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 31);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(100, result.SSS.EE);
        Assert.Equal(200, result.SSS.ER);
        Assert.Equal(10, result.SSS.EC);
        Assert.Equal(210, result.SSS.TotalER);
        Assert.Equal(310, result.SSS.Total);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldDeductFullBalance_OnLastWeekOfWeeklyPayroll()
    {
        // Arrange: Last week of month (Dec 25 - Dec 31)
        var fromDate = new DateOnly(2025, 12, 29);
        var toDate = new DateOnly(2026, 01, 03);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        context.Employee.HireDate = new DateOnly(2025, 12, 16);
        SSSTestHelpers.SetCutoff(context, 15);
        SSSTestHelpers.SetCutoff(context, 31, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);
        // Assert: Divisor should be 1, taking the full remaining balance
        result.SSS.EE.Should().Be(100);
    }

    [Fact]
    public void ShouldHandleMidMonthHire_InSemiMonthly_SecondCutoff()
    {
        // Arrange: Hired Dec 20, Payroll is Dec 16-31
        var fromDate = new DateOnly(2025, 12, 16);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.FixedMonthly,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        context.Employee.HireDate = new DateOnly(2025, 12, 20);
        SSSTestHelpers.SetCutoff(context, 15);
        SSSTestHelpers.SetCutoff(context, 31, true);

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        // Assert: Should NOT divide by 2 because employee wasn't present for 1st cutoff
        result.SSS.EE.Should().Be(100);
    }
}
