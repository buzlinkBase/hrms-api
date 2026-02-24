namespace hrms.test;

using Hrms.Core.Pipelines;
using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using Xunit;

public class SpecialHolidayPipelineTests
{
    [Fact]
    public void Run_ShouldApplySpecialHolidayPolicy_WhenFullDayWorked()
    {

        var context = SpecialHolidayTestHelpers.CreatePayrollContextWithSpecialHoliday(
            dailyRate: 1000,
            specialHolidayHours: 8
        );
        var pipeline = new SpecialHolidayPipeline();
        // Expected: 1.0 × dailyRate
        var expected = 1000m;
        var result = pipeline.Run(context);
        Assert.Equal(expected, result.Value);
    }
    [Fact]
    public void Run_ShouldApplySpecialHolidayPolicy_WhenSpecialHolidayDutyNW()
    {
        var context = SpecialHolidayTestHelpers.CreatePayrollContextWithSpecialHoliday(
            dailyRate: 1_000m,
            specialHolidayHours: 8
        );
        context.WorkType = WorkType.SpecialHolidayDutyNW;
        var pipeline = new SpecialHolidayPipeline();
        var expected = 1_300m;
        var result = pipeline.Run(context);
        Assert.Equal(expected, result.Value);
    }
    [Fact]
    public void Run_ShouldApplySpecialHolidayPolicy_WhenRestDaySpecialHolidayDuty()
    {
        var context = SpecialHolidayTestHelpers.CreatePayrollContextWithSpecialHoliday(
            dailyRate: 1_000m,
            specialHolidayHours: 8
        );
        context.WorkType = WorkType.RestDaySpecialHolidayDuty;
        var pipeline = new SpecialHolidayPipeline();
        var expected = 1_500m;
        var result = pipeline.Run(context);
        Assert.Equal(expected, result.Value);
    }
    //negative scenario
    [Theory]
    [InlineData(4, WorkType.SpecialHolidayDuty, 500)]
    [InlineData(4, WorkType.SpecialHolidayDutyNW, 650)]
    [InlineData(4, WorkType.RestDaySpecialHolidayDuty, 750)]
    public void Run_ShouldApplySpecialHolidayPolicy_WhenhalfDayWorked(int wrkHrs, WorkType workType, decimal expected)
    {

        var context = SpecialHolidayTestHelpers.CreatePayrollContextWithSpecialHoliday(
            dailyRate: 1000,
            specialHolidayHours: wrkHrs
        );
        context.WorkType = workType;
        var pipeline = new SpecialHolidayPipeline();
        var result = pipeline.Run(context);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Run_ShouldApplySpecialHolidayPolicy_WhenNotWorking()
    {
        var context = SpecialHolidayTestHelpers.CreatePayrollContextWithSpecialHoliday(
            dailyRate: 1000,
            specialHolidayHours: 0
        );
        context.WorkType = WorkType.SpecialHoliday;
        var pipeline = new SpecialHolidayPipeline();

        var result = pipeline.Run(context);
        Assert.Equal(0, result.Value);
    }
}

public static class SpecialHolidayTestHelpers
{
    /// <summary>
    /// Creates a PayrollContext for an employee with a specified daily rate
    /// and initializes the required Special Holiday premium rates.
    /// </summary>
    public static PayrollContext CreatePayrollContextWithSpecialHoliday(
        decimal dailyRate = 1000,
        int specialHolidayHours = 0,
        int specialHolidayNDHours = 0)
    {
        var empId = Guid.NewGuid();

        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                Id = empId,
                DailyRate = dailyRate,
            },
            PayrollDate = DateOnly.FromDateTime(DateTime.Today),
            Payload = new CalculatorPayload
            {
                Leaves = new Dictionary<EmployeePayDateKey, List<LeaveApplicationPyRun>>(),
                LeaveCredits = new Dictionary<EmployeeLeaveCreditsKey, decimal>(),
                PremiumRates = TestRateProvider.GetDefaultPremiumRates(),
            },
            DailyRecord = new DailyRecordRunModel
            {
                SpecialHolHours = specialHolidayHours,
                SpecialHolNightDiffHours = specialHolidayNDHours
            },
            WorkType = WorkType.SpecialHolidayDuty
        };
    }
}
