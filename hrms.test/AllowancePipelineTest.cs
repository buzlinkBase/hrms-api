using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using Xunit;

namespace hrms.test;

public class AllowancePipelineTest
{
    [Fact]
    public void ScheduledIncomePolicy_ShouldGroupAndSumCorrectly()
    {
        // Arrange
        var context = AllowanceTestHelpers.CreatePayrollContextWithAllowances(
            regularAllowance: 500,
            otherAllowance: 300,
            commission: 200,
            bonus: 100);

        var pipeline = new AllowancePipeline();

        // Act
        var result = pipeline.Run(context);

        // Assert
        Assert.Equal(1100, result.RunningTotal); // 500 + 300 + 200 + 100
        Assert.Single(result.RegularAllowances);
        Assert.Single(result.OtherIncome);
        Assert.Single(result.Commissions);
        Assert.Single(result.Bonuses);
    }

}

public static class AllowanceTestHelpers
{
    public static PayrollContext CreatePayrollContextWithAllowances(
        decimal regularAllowance = 500,
        decimal otherAllowance = 300,
        decimal commission = 200,
        decimal bonus = 100)
    {
        var empId = Guid.NewGuid();

        // Build income list
        var incomes = new List<OtherIncomeInfo>
        {
            new OtherIncomeInfo
            {
                Id = Guid.NewGuid(),
                IncomeId = Guid.NewGuid(),
                Type = IncomeClassType.Regular,
                PayrollDate = DateOnly.FromDateTime(DateTime.Today),
                Amount = regularAllowance,
                Taxable = true
            },
            new OtherIncomeInfo
            {
                Id = Guid.NewGuid(),
                IncomeId = Guid.NewGuid(),
                Type = IncomeClassType.Others,
                PayrollDate = DateOnly.FromDateTime(DateTime.Today),
                Amount = otherAllowance,
                Taxable = false
            },
            new OtherIncomeInfo
            {
                Id = Guid.NewGuid(),
                IncomeId = Guid.NewGuid(),
                Type = IncomeClassType.Commission,
                PayrollDate = DateOnly.FromDateTime(DateTime.Today),
                Amount = commission,
                Taxable = true
            },
            new OtherIncomeInfo
            {
                Id = Guid.NewGuid(),
                IncomeId = Guid.NewGuid(),
                Type = IncomeClassType.Bonus,
                PayrollDate = DateOnly.FromDateTime(DateTime.Today),
                Amount = bonus,
                Taxable = true
            }
        };

        // Build context with incomes
        var payload = new CalculatorPayload
        {
            Incomes = new Dictionary<EmployeeKey, List<OtherIncomeInfo>>
            {
                { new EmployeeKey(empId), incomes }
            },
            Leaves = new Dictionary<EmployeePayDateKey, List<LeaveApplicationPyRun>>(),
            LeaveCredits = new Dictionary<EmployeeLeaveCreditsKey, decimal>(),
            CompanyPolicy = new CompanyPolicyRule { },
            PremiumRates = TestRateProvider.GetDefaultPremiumRates(),
        };

        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                Id = empId,
                DailyRate = 1000,
                Settings = new EmployeeSettingModel { IsEligibleForOvertime = true }
            },
            PayrollDate = DateOnly.FromDateTime(DateTime.Today),
            Payload = payload,
            DailyRecord = new DailyRecordRunModel(),
            WorkType = WorkType.RegularWorkDay
        };
    }
}
