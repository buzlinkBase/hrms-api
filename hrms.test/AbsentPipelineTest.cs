using Hrms.Core.Specs;
using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using Xunit;

namespace hrms.test;

public class AbsentPipelineTest
{
    public AbsentPipelineTest()
    {

    }
    [Fact]
    public void ApplyIfSatisfied_ShouldMarkEmployeeAbsent_WhenNoLeavesFiled()
    {
        // Arrange
        var context = AbsentTestHelpers.CreatePayrollContextForAbsent(dailyRate: 1000);
        var pipe = new AbsentPipeline();
        // Act
        var result = pipe.Run(context);

        // Assert
        Assert.NotNull(result.AbsentInfo);
        Assert.Equal(1, result.Value);
        Assert.Equal(1000, result.AbsentInfo.Amount);
        Assert.Equal(1, result.AbsentInfo.Count);
        Assert.Equal(WorkType.Absent, context.WorkType);
        Assert.True(new IsAbsent().IsSatisfiedBy(context));
        Assert.False(new IsLeave().IsSatisfiedBy(context));
        Assert.False(new IsRestDay().IsSatisfiedBy(context));
    }

    [Fact]
    public void ApplyIfSatisfied_ShouldNotMarkAbsent_WhenEmployeeHasLeave()
    {
        //dont mark absent if leave is present

        var context = AbsentTestHelpers.CreatePayrollContextForAbsentWithLeave();
        var pipe = new AbsentPipeline();
        var result = pipe.Run(context);

        Assert.Equal(0, result.Value);
        Assert.Equal(0, result.AbsentInfo.Amount);
        Assert.Equal(0, result.AbsentInfo.Count);
        Assert.True(new IsAbsent().IsSatisfiedBy(context));
        Assert.True(new IsLeave().IsSatisfiedBy(context));
        Assert.False(new IsRestDay().IsSatisfiedBy(context));

    }



}
public static class AbsentTestHelpers
{
    /// <summary>
    /// Creates a PayrollContext for an employee who is absent
    /// (no leave applications, no leave credits).
    /// </summary>
    public static PayrollContext CreatePayrollContextForAbsent(decimal dailyRate = 1000)
    {
        var empId = Guid.NewGuid();
        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                Id = empId,
                DailyRate = dailyRate
            },
            PayrollDate = DateOnly.FromDateTime(DateTime.Today),
            WorkType = Hrms.Domain.WorkType.Absent,
            Payload = new CalculatorPayload
            {
                Leaves = new Dictionary<EmployeePayDateKey, List<LeaveApplicationPyRun>>(),
                LeaveCredits = new Dictionary<EmployeeLeaveCreditsKey, decimal>()
            }
        };
    }

    public static PayrollContext CreatePayrollContextForAbsentWithLeave(decimal dailyRate = 1000, DayFraction dayFraction = DayFraction.FullDay)
    {
        var empId = Guid.NewGuid();
        var pdate = DateOnly.FromDateTime(DateTime.Today);
        var leaveId = Guid.NewGuid();
        var leave = LeaveHelper.CreateLeave(empId, leaveId, dayFraction, PayType.WithPay, pdate);
        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                Id = empId,
                DailyRate = dailyRate
            },
            PayrollDate = pdate,
            WorkType = WorkType.Absent,
            Payload = new CalculatorPayload
            {
                PremiumRates = TestRateProvider.GetDefaultPremiumRates(),
                Leaves = new Dictionary<EmployeePayDateKey, List<LeaveApplicationPyRun>>
                {
                    [new EmployeePayDateKey(empId, DateOnly.FromDateTime(DateTime.Today))] = new List<LeaveApplicationPyRun> { leave }
                },
                LeaveCredits = new Dictionary<EmployeeLeaveCreditsKey, decimal>()
                {

                    [new EmployeeLeaveCreditsKey(empId, leaveId)] = 1
                }
            }
        };
    }
}