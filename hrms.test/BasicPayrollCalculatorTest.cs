using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using Xunit;

namespace hrms.test;

public class BasicPayrollCalculatorTest
{


    [Theory]
    [InlineData(WorkType.Absent, false, 1)]
    [InlineData(WorkType.Absent, true, 0)]
    [InlineData(WorkType.PaidLeave, true, 0)]
    [InlineData(WorkType.UnpaidLeave, true, 0)]
    [InlineData(WorkType.PaidLeave, false, 0)]
    [InlineData(WorkType.UnpaidLeave, false, 0)]
    public void ApplyAbsent_WhenWorkTypeIsSet(WorkType workType, bool hasLeave, int expected)
    {
        var calculator = new BasicPayrollCalculator();
        var context = BasicTestHelpers.Create();
        if (hasLeave)
        {
            context = LeaveTestHelpers.CreatePayrollContextWithLeave(true, LeaveDayType.WholeDay, 1000);
        }
        context.WorkType = workType;
        var result = calculator.Calculate(context);
        Assert.Equal(expected, result.AbsentInfo.Count);
    }
    [Fact]
    public void ReturnBasicRate_WhenNoLeave()
    {
        var calculator = new BasicPayrollCalculator();
        var context = BasicTestHelpers.Create(1000);
        context.DailyRecord.RegularNetHours = 8;
        context.DailyRecord.RegularOTHours = 3;
        context.DailyRecord.RegularOTHours = 2;
        context.WorkType = WorkType.LegalHolidayDuty;
        var result = calculator.Calculate(context);
        var expected = 1000m;
        Assert.Equal(expected, result.Basic);
    }
    [Fact]
    public void ReturnBasicRate_WhenLeaveFiled()
    {
        var calculator = new BasicPayrollCalculator();
        var context = LeaveTestHelpers.CreatePayrollContextWithLeave(true, LeaveDayType.WholeDay, 1000);
        context.DailyRecord.RegularNetHours = 0;
        context.WorkType = WorkType.PaidLeave;
        context.Employee.Settings.IsEligibleForLeaveCredits = true;
        var result = calculator.Calculate(context);
        var expected = 1000m;

        Assert.Equal(expected, result.Basic);
        Assert.Equal(1m, result.Leaves.Count());
        Assert.Equal(1, result.Leaves[0].leaveFraction);
        Assert.Equal(1, result.Leaves.Sum(x => x.ConsumeCredit));
    }

    [Fact]
    public void ReturnnoLeave_WhenLegalHoliday()
    {
        var calculator = new BasicPayrollCalculator();
        var context = LeaveTestHelpers.CreatePayrollContextWithLeave(true, LeaveDayType.WholeDay, 1000);
        context.DailyRecord.RegularNetHours = 0;
        context.WorkType = WorkType.LegalHolidayDuty;
        context.Employee.Settings.IsEligibleForHolidayPay = true;
        var result = calculator.Calculate(context);
        var expected = 0m;
        Assert.Equal(expected, result.Basic);
    }

    [Fact]
    public void ShouldComputeBasic_WhenRegularWith2HRNightDiff()
    {
        var calculator = new BasicPayrollCalculator();
        var context = LeaveTestHelpers.CreatePayrollContextWithLeave(true, LeaveDayType.WholeDay, 800);
        context.DailyRecord = new DailyRecordRunModel
        {
            RegularNetHours = 8,
            RegularNDHours = 2,
        };
        context.Employee.Settings = new EmployeeSettingModel
        {
            IsEligibleForOvertime = true,
            IsEligibleForNightDifferential = true,
        };

        context.WorkType = WorkType.RegularWorkDay;
        var result = calculator.Calculate(context);

        Assert.Equal(820, result.TimeBaseGross);
        Assert.Equal(20, result.NightDiffInfo.Amount);
        Assert.Equal(800, result.Basic);
    }

}
public static class BasicTestHelpers
{
    public static PayrollContext Create(decimal dailyRate = 1000)
    {
        var empId = Guid.NewGuid();

        var pydate = DateOnly.FromDateTime(DateTime.Today);
        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                Id = empId,
                DailyRate = dailyRate,
            },
            PayrollDate = pydate,
            Payload = new CalculatorPayload
            {
                Leaves = new Dictionary<EmployeePayDateKey, List<LeaveApplicationPyRun>>(),
                LeaveCredits = new Dictionary<EmployeeLeaveCreditsKey, decimal>(),
                CompanyPolicy = new CompanyPolicyRule { HolidayCreditPolicy = HolidayCreditMode.NoCredit },
                PremiumRates = TestRateProvider.GetDefaultPremiumRates(),
            },
            DailyRecord = new DailyRecordRunModel
            {
                WorkDate = pydate,
            },
            WorkType = WorkType.LegalHolidayDuty
        };
    }


}