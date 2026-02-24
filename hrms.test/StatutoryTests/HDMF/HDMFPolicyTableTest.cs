using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Xunit;

namespace hrms.test;

public class HDMFPolicyTableTest
{
    [Fact]
    public void ShouldComputeHDMF_WhenWithinMonthCutoff()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(500, result.HDMF.EE);
        Assert.Equal(600, result.HDMF.ER);
        Assert.Equal(14500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }
    [Fact]
    public void ShouldComputeMontly_WhenCrossMonth()
    {
        var fromDate = new DateOnly(2025, 12, 26);
        var toDate = new DateOnly(2026, 01, 25);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(500, result.HDMF.EE);
        Assert.Equal(600, result.HDMF.ER);
        Assert.Equal(14500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }
    [Fact]
    public void ShouldComputeMontly_WhenCrossMonthWithPrioContributions()
    {
        var fromDate = new DateOnly(2025, 12, 26);
        var toDate = new DateOnly(2026, 01, 25);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        HDMFTestHelpers.SetCutoff(context, 25, false);
        HDMFTestHelpers.SetContributions(context, 1, 1, 1);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(499, result.HDMF.EE);
        Assert.Equal(599, result.HDMF.ER);
        Assert.Equal(14501m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldReturn_MaxTableRate_WhenAdjustmentMorethanTheTable()
    {
        var fromDate = new DateOnly(2025, 12, 26);
        var toDate = new DateOnly(2026, 01, 25);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        HDMFTestHelpers.SetContributions(context, -100, -100, -10);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        //should not
        Assert.NotNull(result.HDMF);
        Assert.Equal(500, result.HDMF.EE);
        Assert.Equal(600, result.HDMF.ER);
        Assert.Equal(14500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeHDMF_WhenWithinMonthCutoffHigherRate()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 25000,
            dailyRate: 384.61m,
            grossPay: 30000
        );
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(1000, result.HDMF.EE);
        Assert.Equal(2000, result.HDMF.ER);
        Assert.Equal(29000m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }
    //semi

    [Fact]
    public void ShouldComputeMontly_WhenFirstHalf()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 5000
        );

        HDMFTestHelpers.SetCutoff(context, 15);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(250, result.HDMF.EE);
        Assert.Equal(300, result.HDMF.ER);
        Assert.Equal(4750m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeHDMFContribution_For2ndHalfNoPrioContribution()
    {
        var fromDate = new DateOnly(2025, 12, 16);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        HDMFTestHelpers.SetCutoff(context, 15);
        HDMFTestHelpers.SetCutoff(context, 31, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(500, result.HDMF.EE);
        Assert.Equal(600, result.HDMF.ER);
        Assert.Equal(14500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeHDMFContribution_For2ndHalfWithPriorContribution()
    {
        var fromDate = new DateOnly(2025, 12, 16);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        HDMFTestHelpers.SetCutoff(context, 31, true);
        HDMFTestHelpers.SetContributions(context, 250, 300, 10);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(250, result.HDMF.EE);
        Assert.Equal(300, result.HDMF.ER);
        Assert.Equal(14750m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeHDMFContribution_ForTableBasisMonthlyVariableSalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 06);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(100, result.HDMF.EE);
        Assert.Equal(120, result.HDMF.ER);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeHDMFContribution_ForTableBasisMonthlyVariableSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 16);
        var toDate = new DateOnly(2025, 12, 16);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 454.54m,
            grossPay: 10_000
        );

        context.Payload.Payrolls[new EmployeeKey(context.Employee.Id)] = new List<Payroll>
        {
            new Hrms.Domain.Entities.HR.Payroll
            {
                Id = context.Employee.Id,
                PayrollDate = fromDate.AddDays(15),
            }
        };
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(51.61m, Math.Round(result.HDMF.EE, 2));
        Assert.Equal(103.23m, Math.Round(result.HDMF.ER, 2));
        Assert.Equal(9948.39m, Math.Round(result.RemainingGrossBalance, 2));
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// MONTHLY_FIXED
    /// </summary>
    [Fact]
    public void ShouldComputeHDMFContribution_ForTableBasisMonthlyTableSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_FIXED,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 9_999,
            dailyRate: 384.61m,
            grossPay: 9_999
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(100, result.HDMF.EE);
        Assert.Equal(200, result.HDMF.ER);
        Assert.Equal(9_899m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForTableBasisMonthlyTableSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 16);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_FIXED,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 9999,
            dailyRate: 384.61m,
            grossPay: 9999
        );

        HDMFTestHelpers.SetCutoff(context, 31, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(50, result.HDMF.EE);
        Assert.Equal(100, result.HDMF.ER);
        Assert.Equal(9_949m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public async Task ShouldComputeHDMFContribution_ForTableBasisMonthlyTableSalaryWeeklyPayroll()
    {
        //cross month without prior contribution
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 6);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_FIXED,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(100, result.HDMF.EE);
        Assert.Equal(120, result.HDMF.ER);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }


    [Fact]
    public void ShouldComputeHDMFContribution_ForTableBasisMonthlyTableSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 01);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_FIXED,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(3.23m, Math.Round(result.HDMF.EE, 2));
        Assert.Equal(6.45m, Math.Round(result.HDMF.ER, 2));
        Assert.Equal(14996.77m, Math.Round(result.RemainingGrossBalance, 2));
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// DAILY
    /// </summary>
    [Fact]
    public void ShouldComputeHDMFContribution_ForTableBasisDailySalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(500, result.HDMF.EE);
        Assert.Equal(600, result.HDMF.ER);
        Assert.Equal(14_500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }


    [Fact]
    public void ShouldComputeHDMFContribution_ForTableBasisDailySalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        HDMFTestHelpers.SetCutoff(context, 15);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(250, result.HDMF.EE);
        Assert.Equal(300, result.HDMF.ER);
        Assert.Equal(14_750m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForTableBasisDailySalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 22);
        var toDate = new DateOnly(2025, 12, 26);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15000
        );


        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.HDMF);
        Assert.Equal(100, result.HDMF.EE);
        Assert.Equal(120, result.HDMF.ER);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }

    [Fact]
    public void ShouldComputeHDMFContribution_ForTableBasisDailySalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 31);
        var toDate = new DateOnly(2025, 12, 31);
        var context = HDMFTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.Table,
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
