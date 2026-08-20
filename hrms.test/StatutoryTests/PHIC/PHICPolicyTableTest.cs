//using Hrms.Domain;
//using Hrms.Domain.Entities;
//using Hrms.Domain.ValueObjects;
//using Xunit;

//namespace hrms.test;

//public class PHICPolicyTableTest
//{
//    [Fact]
//    public void ShouldComputePHIC_WhenWithinMonthCutoff()
//    {
//        var fromDate = new DateOnly(2025, 12, 1);
//        var toDate = new DateOnly(2025, 12, 31);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.VARIABLE,
//            payrollFrequency: PayrollFrequency.MONTHLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m,
//            grossPay: 15_000
//        );
//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(500, result.PHIC.EE);
//        Assert.Equal(600, result.PHIC.ER);
//        Assert.Equal(14500m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);
//    }
//    [Fact]
//    public void ShouldComputeMontly_WhenCrossMonth()
//    {
//        var fromDate = new DateOnly(2025, 12, 26);
//        var toDate = new DateOnly(2026, 01, 25);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.VARIABLE,
//            payrollFrequency: PayrollFrequency.MONTHLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m,
//            grossPay: 15_000
//        );
//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(500, result.PHIC.EE);
//        Assert.Equal(600, result.PHIC.ER);
//        Assert.Equal(14500m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);
//    }

//    [Fact]
//    public void ShouldComputeMontly_WhenCrossMonthWithPrioContributions()
//    {
//        var fromDate = new DateOnly(2025, 12, 26);
//        var toDate = new DateOnly(2026, 01, 25);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.VARIABLE,
//            payrollFrequency: PayrollFrequency.MONTHLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m,
//            grossPay: 15_000
//        );
//        PHICTestHelpers.SetCutoff(context, 25, false);
//        PHICTestHelpers.SetContributions(context, 1, 1, 1);
//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(499, result.PHIC.EE);
//        Assert.Equal(599, result.PHIC.ER);
//        Assert.Equal(14501m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);
//    }

//    [Fact]
//    public void ShouldReturn_MaxTableRate_WhenAdjustmentMorethanTheTable()
//    {
//        var fromDate = new DateOnly(2025, 12, 26);
//        var toDate = new DateOnly(2026, 01, 25);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.VARIABLE,
//            payrollFrequency: PayrollFrequency.MONTHLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m,
//            grossPay: 15_000
//        );
//        PHICTestHelpers.SetContributions(context, -100, -100, -10);
//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        //should not
//        Assert.NotNull(result.PHIC);
//        Assert.Equal(500, result.PHIC.EE);
//        Assert.Equal(600, result.PHIC.ER);
//        Assert.Equal(14500m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);
//    }

//    [Fact]
//    public void ShouldComputePHIC_WhenWithinMonthCutoffHigherRate()
//    {
//        var fromDate = new DateOnly(2025, 12, 1);
//        var toDate = new DateOnly(2025, 12, 31);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.VARIABLE,
//            payrollFrequency: PayrollFrequency.MONTHLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 25000,
//            dailyRate: 384.61m,
//            grossPay: 30000
//        );
//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(1000, result.PHIC.EE);
//        Assert.Equal(2000, result.PHIC.ER);
//        Assert.Equal(29000m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);
//    }
//    //semi

//    [Fact]
//    public void ShouldComputeMontly_WhenFirstHalf()
//    {
//        var fromDate = new DateOnly(2025, 12, 1);
//        var toDate = new DateOnly(2025, 12, 15);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.VARIABLE,
//            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m,
//            grossPay: 5000
//        );

//        PHICTestHelpers.SetCutoff(context, 15);
//        PHICTestHelpers.SetCutoff(context, 31, true);
//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(250, result.PHIC.EE);
//        Assert.Equal(300, result.PHIC.ER);
//        Assert.Equal(4750m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);
//    }


//    [Fact]
//    public void ShouldComputePHICContribution_For2ndHalfNoPrioContribution()
//    {
//        var fromDate = new DateOnly(2025, 12, 16);
//        var toDate = new DateOnly(2025, 12, 31);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.VARIABLE,
//            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m,
//            grossPay: 15_000
//        );

//        HDMFTestHelpers.SetCutoff(context, 15);
//        HDMFTestHelpers.SetCutoff(context, 31, true);

//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(500, result.PHIC.EE);
//        Assert.Equal(600, result.PHIC.ER);
//        Assert.Equal(14500m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);
//    }

//    [Fact]
//    public void ShouldComputePHICContribution_For2ndHalfWithPriorContribution()
//    {
//        var fromDate = new DateOnly(2025, 12, 16);
//        var toDate = new DateOnly(2025, 12, 31);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.VARIABLE,
//            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m,
//            grossPay: 15_000
//        );

//        HDMFTestHelpers.SetCutoff(context, 15);
//        HDMFTestHelpers.SetCutoff(context, 31, true);
//        PHICTestHelpers.SetContributions(context, 250, 300, 10);
//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(250, result.PHIC.EE);
//        Assert.Equal(300, result.PHIC.ER);
//        Assert.Equal(14750m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);
//    }


//    [Fact]
//    public void ShouldComputePHICContribution_ForTableBasisMonthlyVariableSalaryWeeklyPayroll()
//    {
//        var fromDate = new DateOnly(2025, 12, 01);
//        var toDate = new DateOnly(2025, 12, 06);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.VARIABLE,
//            payrollFrequency: PayrollFrequency.WEEKLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m,
//            grossPay: 15_000
//        );

//        PHICTestHelpers.SetCutoff(context, 7, false);
//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(100, result.PHIC.EE);
//        Assert.Equal(120, result.PHIC.ER);
//        Assert.Equal(14_900m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);
//    }


//    [Fact]
//    public void ShouldComputePHICContribution_ForTableBasisMonthlyVariableSalaryDailyPayroll()
//    {
//        var fromDate = new DateOnly(2025, 12, 16);
//        var toDate = new DateOnly(2025, 12, 16);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.VARIABLE,
//            payrollFrequency: PayrollFrequency.DAILY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 454.54m,
//            grossPay: 10_000
//        );

//        context.Payload.Payrolls[new EmployeeKey(context.Employee.Id)] = new List<Payroll>
//        {
//            new  Payroll
//            {
//                Id = context.Employee.Id,
//                PayrollDate = fromDate.AddDays(15),
//            }
//        };
//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(51.61m, Math.Round(result.PHIC.EE, 2));
//        Assert.Equal(103.23m, Math.Round(result.PHIC.ER, 2));
//        Assert.Equal(9948.39m, Math.Round(result.RemainingGrossBalance, 2));
//        Assert.Empty(result.ScheduledDeductions);
//    }

//    /// <summary>
//    /// MONTHLY_FIXED
//    /// </summary>
//    [Fact]
//    public void ShouldComputePHICContribution_ForTableBasisMonthlyTableSalaryMonthlyPayroll()
//    {
//        var fromDate = new DateOnly(2025, 12, 1);
//        var toDate = new DateOnly(2025, 12, 31);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.FIXED,
//            payrollFrequency: PayrollFrequency.MONTHLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 9_999,
//            dailyRate: 384.61m,
//            grossPay: 9_999
//        );

//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(100, result.PHIC.EE);
//        Assert.Equal(200, result.PHIC.ER);
//        Assert.Equal(9_899m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);
//    }

//    [Fact]
//    public void ShouldComputePHICContribution_ForTableBasisMonthlyTableSalarySemiMonthlyPayroll()
//    {
//        var fromDate = new DateOnly(2025, 12, 16);
//        var toDate = new DateOnly(2025, 12, 31);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.FIXED,
//            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 9999,
//            dailyRate: 384.61m,
//            grossPay: 9999
//        );

//        PHICTestHelpers.SetCutoff(context, 31, true);
//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(50, result.PHIC.EE);
//        Assert.Equal(100, result.PHIC.ER);
//        Assert.Equal(9_949m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);
//    }

//    [Fact]
//    public async Task ShouldComputePHICContribution_ForTableBasisMonthlyTableSalaryWeeklyPayroll()
//    {
//        //cross month without prior contribution
//        var fromDate = new DateOnly(2025, 12, 1);
//        var toDate = new DateOnly(2025, 12, 6);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.FIXED,
//            payrollFrequency: PayrollFrequency.WEEKLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10000,
//            dailyRate: 384.61m,
//            grossPay: 15_000
//        );

//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(100, result.PHIC.EE);
//        Assert.Equal(120, result.PHIC.ER);
//        Assert.Equal(14_900m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);

//    }


//    [Fact]
//    public void ShouldComputePHICContribution_ForTableBasisMonthlyTableSalaryDailyPayroll()
//    {
//        var fromDate = new DateOnly(2025, 12, 01);
//        var toDate = new DateOnly(2025, 12, 01);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.FIXED,
//            payrollFrequency: PayrollFrequency.DAILY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m,
//            grossPay: 15_000
//        );

//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(3.23m, Math.Round(result.PHIC.EE, 2));
//        Assert.Equal(6.45m, Math.Round(result.PHIC.ER, 2));
//        Assert.Equal(14996.77m, Math.Round(result.RemainingGrossBalance, 2));
//        Assert.Empty(result.ScheduledDeductions);
//    }

//    /// <summary>
//    /// DAILY
//    /// </summary>
//    [Fact]
//    public void ShouldComputePHICContribution_ForTableBasisDailySalaryMonthlyPayroll()
//    {
//        var fromDate = new DateOnly(2025, 12, 1);
//        var toDate = new DateOnly(2025, 12, 31);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.DAILY,
//            payrollFrequency: PayrollFrequency.MONTHLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m,
//            grossPay: 15_000
//        );
//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(500, result.PHIC.EE);
//        Assert.Equal(600, result.PHIC.ER);
//        Assert.Equal(14_500m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);

//    }


//    [Fact]
//    public void ShouldComputePHICContribution_ForTableBasisDailySalarySemiMonthlyPayroll()
//    {
//        var fromDate = new DateOnly(2025, 12, 1);
//        var toDate = new DateOnly(2025, 12, 15);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.DAILY,
//            payrollFrequency: PayrollFrequency.SEMI_MONTHLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m,
//            grossPay: 15_000
//        );
//        PHICTestHelpers.SetCutoff(context, 15);
//        PHICTestHelpers.SetCutoff(context, 31, true);
//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(250, result.PHIC.EE);
//        Assert.Equal(300, result.PHIC.ER);
//        Assert.Equal(14_750m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);

//    }

//    [Fact]
//    public void ShouldComputePHICContribution_ForTableBasisDailySalaryWeeklyPayroll()
//    {
//        var fromDate = new DateOnly(2025, 12, 22);
//        var toDate = new DateOnly(2025, 12, 26);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.DAILY,
//            payrollFrequency: PayrollFrequency.WEEKLY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m,
//            grossPay: 15000
//        );


//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(100, result.PHIC.EE);
//        Assert.Equal(120, result.PHIC.ER);
//        Assert.Equal(14_900m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);

//    }

//    [Fact]
//    public void ShouldComputePHICContribution_ForTableBasisDailySalaryDailyPayroll()
//    {
//        var fromDate = new DateOnly(2025, 12, 31);
//        var toDate = new DateOnly(2025, 12, 31);
//        var context = PHICTestHelpers.BuildContext(
//            fromDate: fromDate,
//            toDate: toDate,
//            salaryType: SalaryType.DAILY,
//            payrollFrequency: PayrollFrequency.DAILY,
//            computationBasis: ComputationBasis.Table,
//            monthlyRate: 10_000,
//            dailyRate: 384.61m, // divisor 26 assumed
//            grossPay: 15_000
//        );

//        var pipeline = new DeductionPipeline();
//        var result = pipeline.Run(context);

//        Assert.NotNull(result.PHIC);
//        Assert.Equal(100, result.PHIC.EE);
//        Assert.Equal(200, result.PHIC.ER);
//        Assert.Equal(14_900m, result.RemainingGrossBalance);
//        Assert.Empty(result.ScheduledDeductions);
//    }

//}
