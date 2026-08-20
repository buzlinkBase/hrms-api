using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Xunit;

namespace hrms.test;

public class WTaxPolicyTableTest
{
    [Fact]
    public void ShouldComputeWTax_WhenWithinMonthCutoff()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        WTaxTestHelpers.SetCutoff(context, 1, true);

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(500, result.TaxInfo.TaxDue);
        Assert.Equal(14500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }
    [Fact]
    public void ShouldComputeMontly_WhenCrossMonth()
    {
        var fromDate = new DateOnly(2025, 12, 26);
        var toDate = new DateOnly(2026, 01, 25);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(500, result.TaxInfo.TaxDue);
        Assert.Equal(14500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }
    [Fact]
    public void ShouldComputeMontly_WhenCrossMonthWithPrioContributions()
    {
        var fromDate = new DateOnly(2025, 12, 26);
        var toDate = new DateOnly(2026, 01, 25);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        WTaxTestHelpers.SetContributions(context, 1);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(499, result.TaxInfo.TaxDue);
        Assert.Equal(14501m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldReturn_MaxTableRate_WhenAdjustmentMorethanTheTable()
    {
        var fromDate = new DateOnly(2025, 12, 26);
        var toDate = new DateOnly(2026, 01, 25);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );
        WTaxTestHelpers.SetContributions(context, -100);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        //should not
        Assert.NotNull(result.TaxInfo);
        Assert.Equal(500, result.TaxInfo.TaxDue);
        Assert.Equal(14500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeWTax_WhenWithinMonthCutoffHigherRate()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 25000,
            dailyRate: 384.61m,
            grossPay: 30000
        );
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(1000, result.TaxInfo.TaxDue);
        Assert.Equal(29000m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }
    //semi

    [Fact]
    public void ShouldComputeMontly_WhenFirstHalf()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 5000
        );

        WTaxTestHelpers.SetCutoff(context, 1, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(250, result.TaxInfo.TaxDue);
        Assert.Equal(4750m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeWTaxContribution_For2ndHalfNoPrioContribution()
    {
        var fromDate = new DateOnly(2025, 12, 16);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        WTaxTestHelpers.SetCutoff(context, 15);
        WTaxTestHelpers.SetCutoff(context, 31, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(500, result.TaxInfo.TaxDue);
        Assert.Equal(14500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeWTaxContribution_For2ndHalfWithPriorContribution()
    {
        var fromDate = new DateOnly(2025, 12, 16);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        HDMFTestHelpers.SetCutoff(context, 15);
        HDMFTestHelpers.SetCutoff(context, 31, true);
        WTaxTestHelpers.SetContributions(context, 250);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(250, result.TaxInfo.TaxDue);
        Assert.Equal(14750m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeWTaxContribution_ForTableBasisMonthlyVariableSalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 11, 01);
        var toDate = new DateOnly(2025, 11, 01);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        WTaxTestHelpers.SetCutoff(context, 6, false);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(100, result.TaxInfo.TaxDue);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }


    [Fact]
    public void ShouldComputeWTaxContribution_ForTableBasisMonthlyVariableSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 16);
        var toDate = new DateOnly(2025, 12, 16);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.VARIABLE,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 454.54m,
            grossPay: 10_000
        );

        context.Payload.Payrolls[new EmployeeKey(context.Employee.Id)] = new List<Payroll>
        {
            new Payroll
            {
                Id = context.Employee.Id,
                PayrollDate = fromDate.AddDays(15),
            }
        };
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(51.61m, Math.Round(result.TaxInfo.TaxDue, 2));
        Assert.Equal(9948.39m, Math.Round(result.RemainingGrossBalance, 2));
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// MONTHLY_FIXED
    /// </summary>
    [Fact]
    public void ShouldComputeWTaxContribution_ForTableBasisMonthlyTableSalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 9_999,
            dailyRate: 384.61m,
            grossPay: 9_999
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(100, result.TaxInfo.TaxDue);
        Assert.Equal(9_899m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public void ShouldComputeWTaxContribution_ForTableBasisMonthlyTableSalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 9999,
            dailyRate: 384.61m,
            grossPay: 9999
        );
        WTaxTestHelpers.SetCutoff(context, 1, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(50, result.TaxInfo.TaxDue);
        Assert.Equal(9_949m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

    [Fact]
    public async Task ShouldComputeWTaxContribution_ForTableBasisMonthlyTableSalaryWeeklyPayroll()
    {
        //cross month without prior contribution
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 6);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        WTaxTestHelpers.SetCutoff(context, 6, false);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(100, result.TaxInfo.TaxDue);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }


    [Fact]
    public void ShouldComputeWTaxContribution_ForTableBasisMonthlyTableSalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 01);
        var toDate = new DateOnly(2025, 12, 01);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.FIXED,
            payrollFrequency: PayrollFrequency.DAILY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(3.23m, Math.Round(result.TaxInfo.TaxDue, 2));
        Assert.Equal(14996.77m, Math.Round(result.RemainingGrossBalance, 2));
        Assert.Empty(result.ScheduledDeductions);
    }

    /// <summary>
    /// DAILY
    /// </summary>
    [Fact]
    public void ShouldComputeWTaxContribution_ForTableBasisDailySalaryMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
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

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(500, result.TaxInfo.TaxDue);
        Assert.Equal(14_500m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }


    [Fact]
    public void ShouldComputeWTaxContribution_ForTableBasisDailySalarySemiMonthlyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 1);
        var toDate = new DateOnly(2025, 12, 15);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15_000
        );

        WTaxTestHelpers.SetCutoff(context, 1, true);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(250, result.TaxInfo.TaxDue);
        Assert.Equal(14_750m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }

    [Fact]
    public void ShouldComputeWTaxContribution_ForTableBasisDailySalaryWeeklyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 22);
        var toDate = new DateOnly(2025, 12, 26);
        var context = WTaxTestHelpers.BuildContext(
            fromDate: fromDate,
            toDate: toDate,
            salaryType: SalaryType.DAILY,
            payrollFrequency: PayrollFrequency.WEEKLY,
            computationBasis: ComputationBasis.Table,
            monthlyRate: 10_000,
            dailyRate: 384.61m,
            grossPay: 15000
        );

        WTaxTestHelpers.SetCutoff(context, 6, false);
        var pipeline = new DeductionPipeline();
        var result = pipeline.Run(context);

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(100, result.TaxInfo.TaxDue);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);

    }

    [Fact]
    public void ShouldComputeWTaxContribution_ForTableBasisDailySalaryDailyPayroll()
    {
        var fromDate = new DateOnly(2025, 12, 31);
        var toDate = new DateOnly(2025, 12, 31);
        var context = WTaxTestHelpers.BuildContext(
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

        Assert.NotNull(result.TaxInfo);
        Assert.Equal(100, result.TaxInfo.TaxDue);
        Assert.Equal(14_900m, result.RemainingGrossBalance);
        Assert.Empty(result.ScheduledDeductions);
    }

}
