using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Xunit;

namespace hrms.test;

public class SSSPolicyTableTest
{
    [Fact]
    public void ShouldComputeSSS_WhenWithinMonthCutoff()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        SSSTestHelpers.SetCutoff(context, 1, true);

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(500, result.SSS.EE);
        Assert.Equal(600, result.SSS.ER);
        Assert.Equal(20, result.SSS.EC);
        Assert.Equal(620, result.SSS.TotalER);
        Assert.Equal(1120, result.SSS.Total);
        Assert.Equal(14500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }
    [Fact]
    public void ShouldComputeMontly_WhenCrossMonth()
    {
        var fromDate = new DateOnly(2025, 12, 26);
        var toDate = new DateOnly(2026, 01, 25);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(500, result.SSS.EE);
        Assert.Equal(600, result.SSS.ER);
        Assert.Equal(20, result.SSS.EC);
        Assert.Equal(620, result.SSS.TotalER);
        Assert.Equal(1120, result.SSS.Total);
        Assert.Equal(14500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }
    [Fact]
    public void ShouldComputeMontly_WhenCrossMonthWithPrioContributions()
    {
        var fromDate = new DateOnly(2025, 12, 26);
        var toDate = new DateOnly(2026, 01, 25);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        SSSTestHelpers.SetContributions(context, 1, 1, 1);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(499, result.SSS.EE);
        Assert.Equal(599, result.SSS.ER);
        Assert.Equal(19, result.SSS.EC);
        Assert.Equal(618, result.SSS.TotalER);
        Assert.Equal(1117, result.SSS.Total);
        Assert.Equal(14501m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldReturn_MaxTableRate_WhenAdjustmentMorethanTheTable()
    {
        var fromDate = new DateOnly(2025, 12, 26);
        var toDate = new DateOnly(2026, 01, 25);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        SSSTestHelpers.SetContributions(context, -100, -100, -10);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        //should not
        Assert.NotNull(result.SSS);
        Assert.Equal(500, result.SSS.EE);
        Assert.Equal(600, result.SSS.ER);
        Assert.Equal(20, result.SSS.EC);
        Assert.Equal(620, result.SSS.TotalER);
        Assert.Equal(1120, result.SSS.Total);
        Assert.Equal(14500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeSSS_WhenWithinMonthCutoffHigherRate()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(1000, result.SSS.EE);
        Assert.Equal(2000, result.SSS.ER);
        Assert.Equal(50, result.SSS.EC);
        Assert.Equal(2050, result.SSS.TotalER);
        Assert.Equal(3050, result.SSS.Total);
        Assert.Equal(29000m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }
    //semi

    [Fact]
    public void ShouldComputeMontly_WhenFirstHalf()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 5000
        );

        SSSTestHelpers.SetCutoff(context, 1, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(250, result.SSS.EE);
        Assert.Equal(300, result.SSS.ER);
        Assert.Equal(10, result.SSS.EC);
        Assert.Equal(310, result.SSS.TotalER);
        Assert.Equal(560, result.SSS.Total);
        Assert.Equal(4750m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeSSSContribution_For2ndHalfNoPrioContribution()
    {
        var fromDate = new DateOnly(2025, 12, 16);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        SSSTestHelpers.SetCutoff(context, 15);
        SSSTestHelpers.SetCutoff(context, 31, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(500, result.SSS.EE);
        Assert.Equal(600, result.SSS.ER);
        Assert.Equal(20, result.SSS.EC);
        Assert.Equal(620, result.SSS.TotalER);
        Assert.Equal(1120, result.SSS.Total);
        Assert.Equal(14500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeSSSContribution_For2ndHalfWithPriorContribution()
    {
        var fromDate = new DateOnly(2025, 12, 16);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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
        SSSTestHelpers.SetContributions(context, 250, 300, 10);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(250, result.SSS.EE);
        Assert.Equal(300, result.SSS.ER);
        Assert.Equal(10, result.SSS.EC);
        Assert.Equal(310, result.SSS.TotalER);
        Assert.Equal(560, result.SSS.Total);
        Assert.Equal(14750m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeSSSContribution_ForTableBasisMonthlyVariableSalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 06);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_VARIABLE,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        SSSTestHelpers.SetCutoff(context, 6, false);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(100, result.SSS.EE);
        Assert.Equal(120, result.SSS.ER);
        Assert.Equal(4, result.SSS.EC);
        Assert.Equal(124, result.SSS.TotalER);
        Assert.Equal(224, result.SSS.Total);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeSSSContribution_ForTableBasisMonthlyVariableSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 16);
        var toDate = new DateOnly(2025, 12, 16);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(51.61m, Math.Round(result.SSS.EE, 2));
        Assert.Equal(103.23m, Math.Round(result.SSS.ER, 2));
        Assert.Equal(5.16m, Math.Round(result.SSS.EC, 2));
        Assert.Equal(108.39m, Math.Round(result.SSS.TotalER, 2));
        Assert.Equal(160.0m, Math.Round(result.SSS.Total, 2));
        Assert.Equal(9948.39m, Math.Round(result.RemainingGrossBalance, 2));
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// MONTHLY_FIXED
    /// </summary>
    [Fact]
    public void ShouldComputeSSSContribution_ForTableBasisMonthlyTableSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(100, result.SSS.EE);
        Assert.Equal(200, result.SSS.ER);
        Assert.Equal(10, result.SSS.EC);
        Assert.Equal(210, result.SSS.TotalER);
        Assert.Equal(310, result.SSS.Total);
        Assert.Equal(9_899m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeSSSContribution_ForTableBasisMonthlyTableSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_FIXED,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 9999,
            dailyRate: 384.61m,
            grossPay: 9999
        );
        SSSTestHelpers.SetCutoff(context, 1, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(50, result.SSS.EE);
        Assert.Equal(100, result.SSS.ER);
        Assert.Equal(5, result.SSS.EC);
        Assert.Equal(105, result.SSS.TotalER);
        Assert.Equal(155, result.SSS.Total);
        Assert.Equal(9_949m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public async Task ShouldComputeSSSContribution_ForTableBasisMonthlyTableSalaryWeeklyPayroll()
    {
        //cross month without prior contribution
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 6);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.MONTHLY_FIXED,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        SSSTestHelpers.SetCutoff(context, 6, false);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(100, result.SSS.EE);
        Assert.Equal(120, result.SSS.ER);
        Assert.Equal(4, result.SSS.EC);
        Assert.Equal(124, result.SSS.TotalER);
        Assert.Equal(224, result.SSS.Total);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }


    [Fact]
    public void ShouldComputeSSSContribution_ForTableBasisMonthlyTableSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 01);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(3.23m, Math.Round(result.SSS.EE, 2));
        Assert.Equal(6.45m, Math.Round(result.SSS.ER, 2));
        Assert.Equal(0.32m, Math.Round(result.SSS.EC, 2));
        Assert.Equal(6.77m, Math.Round(result.SSS.TotalER, 2));
        Assert.Equal(10.0m, Math.Round(result.SSS.Total, 2));
        Assert.Equal(14996.77m, Math.Round(result.RemainingGrossBalance, 2));
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// DAILY
    /// </summary>
    [Fact]
    public void ShouldComputeSSSContribution_ForTableBasisDailySalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(500, result.SSS.EE);
        Assert.Equal(600, result.SSS.ER);
        Assert.Equal(20, result.SSS.EC);
        Assert.Equal(620, result.SSS.TotalER);
        Assert.Equal(1120, result.SSS.Total);
        Assert.Equal(14_500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }


    [Fact]
    public void ShouldComputeSSSContribution_ForTableBasisDailySalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        SSSTestHelpers.SetCutoff(context, 1, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(250, result.SSS.EE);
        Assert.Equal(300, result.SSS.ER);
        Assert.Equal(10, result.SSS.EC);
        Assert.Equal(310, result.SSS.TotalER);
        Assert.Equal(560, result.SSS.Total);
        Assert.Equal(14_750m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }

    [Fact]
    public void ShouldComputeSSSContribution_ForTableBasisDailySalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 22);
        var toDate = new DateOnly(2025, 12, 26);
        var context = SSSTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15000
        );

        SSSTestHelpers.SetCutoff(context, 6, false);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.SSS);
        Assert.Equal(100, result.SSS.EE);
        Assert.Equal(120, result.SSS.ER);
        Assert.Equal(4, result.SSS.EC);
        Assert.Equal(124, result.SSS.TotalER);
        Assert.Equal(224, result.SSS.Total);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }

    [Fact]
    public void ShouldComputeSSSContribution_ForTableBasisDailySalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 31);
        var toDate = new DateOnly(2025, 12, 31);
        var context = SSSTestHelpers.BuildContext(
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

        Assert.NotNull(result.SSS);
        Assert.Equal(100, result.SSS.EE);
        Assert.Equal(200, result.SSS.ER);
        Assert.Equal(10, result.SSS.EC);
        Assert.Equal(210, result.SSS.TotalER);
        Assert.Equal(310, result.SSS.Total);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

}
