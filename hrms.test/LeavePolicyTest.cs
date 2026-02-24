namespace hrms.test;

using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using Xunit;

public class LeavePipelineTest
{
    [Fact]
    public void ApplyIfSatified_NoLeaves_ReturnsOriginalCollection()
    {
        var context = LeaveTestHelpers.CreatePayrollContextWithNoLeaves();
        var pipe = new LeavePipeline();

        var result = pipe.Run(context);

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyIfSatified_LeaveWithoutCredits_ConvertsToUnpaid()
    {
        var context = LeaveTestHelpers.CreatePayrollContextWithLeave(hasCredits: false);
        var pipe = new LeavePipeline();
        var result = pipe.Run(context);

        var line = Assert.Single(result);
        Assert.Equal(PayType.WithoutPay, line.PayType);
        Assert.Equal(0, line.Value);
        Assert.Equal(0, line.LeaveInfo.ConsumeCredit);
    }

    [Fact]
    public void ApplyIfSatified_WholeDayLeaveWithCredits_ComputesValue()
    {
        var context = LeaveTestHelpers.CreatePayrollContextWithLeave(hasCredits: true, dayType: LeaveDayType.WholeDay, dailyRate: 1000);
        var pipe = new LeavePipeline();
        var result = pipe.Run(context);

        var line = Assert.Single(result);
        Assert.Equal(PayType.WithPay, line.PayType);
        Assert.Equal(1000, line.Value);
        Assert.Equal(1.0, line.LeaveInfo.ConsumeCredit);
    }

    [Fact]
    public void ApplyIfSatified_HalfDayLeaveWithCredits_ComputesHalfValue()
    {
        var context = LeaveTestHelpers.CreatePayrollContextWithLeave(hasCredits: true, dayType: LeaveDayType.HalfDay, dailyRate: 800);
        var pipe = new LeavePipeline();
        var result = pipe.Run(context);

        var line = Assert.Single(result);
        Assert.Equal(400, line.Value); // half of daily rate
        Assert.Equal(0.5, line.LeaveInfo.ConsumeCredit);
    }


    [Fact]
    public void ApplyIfSatified_MultipleLeaves_AllProcessed()
    {
        var context = LeaveTestHelpers.CreatePayrollContextWithMultipleLeaves();
        var pipe = new LeavePipeline();
        var result = pipe.Run(context);
        Assert.True(result.Count >= 2);
        Assert.True(result.Count >= 2);
    }

    [Fact]
    public void ApplyIfSatified_MultipleDoubleHalfDayLeaves()
    {
        var context = LeaveTestHelpers.CreatePayrollContextWithMultipleLeaves();
        var pipe = new LeavePipeline();
        var result = pipe.Run(context);
        Assert.True(result.Count >= 2);
        Assert.True(result.Count >= 2);
    }

}

public static class LeaveTestHelpers
{
    public static PayrollContext CreatePayrollContextWithNoLeaves()
    {
        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun { Id = Guid.NewGuid(), DailyRate = 1000 },
            PayrollDate = DateOnly.FromDateTime(DateTime.Today),
            Payload = new CalculatorPayload
            {
                Leaves = new Dictionary<EmployeePayDateKey, List<LeaveApplicationPyRun>>(),
                LeaveCredits = new Dictionary<EmployeeLeaveCreditsKey, decimal>(),
            }
        };
    }

    public static PayrollContext CreatePayrollContextWithLeave(
        bool hasCredits, LeaveDayType dayType = LeaveDayType.WholeDay,
        decimal dailyRate = 1000,
        PayType payType = PayType.WithPay)
    {
        var empId = Guid.NewGuid();
        var leaveId = Guid.NewGuid();

        var employee = new EmployeeModelPayrollRun
        {
            Id = empId,
            DailyRate = dailyRate,
            Settings = new EmployeeSettingModel { IsEligibleForLeaveCredits = true }
        };
        var leave = LeaveHelper.CreateLeave(empId, leaveId, dayType, payType);

        return new PayrollContext
        {
            Employee = employee,
            PayrollDate = DateOnly.FromDateTime(DateTime.Today),
            Payload = new CalculatorPayload
            {
                PremiumRates = TestRateProvider.GetDefaultPremiumRates(),
                Leaves = new Dictionary<EmployeePayDateKey, List<LeaveApplicationPyRun>>
                {
                    [new EmployeePayDateKey(empId, DateOnly.FromDateTime(DateTime.Today))] = new List<LeaveApplicationPyRun> { leave }
                },
                LeaveCredits = LeaveCreditsHelper.CreateLeaveCredits(empId, new[]
                {
                    (leaveId, hasCredits ? 5m  : 0m )
                }),
            }
        };
    }

    public static PayrollContext CreatePayrollContextWithMultipleLeaves()
    {
        var empId = Guid.NewGuid();
        var leaveId1 = Guid.NewGuid();
        var leaveId2 = Guid.NewGuid();

        var leave1 = LeaveHelper.CreateLeave(empId, leaveId1, LeaveDayType.HalfDay);
        var leave2 = LeaveHelper.CreateLeave(empId, leaveId2, LeaveDayType.HalfDay);

        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                Id = empId,
                DailyRate = 1000,
                Settings = new EmployeeSettingModel() { IsEligibleForLeaveCredits = true }
            },
            PayrollDate = DateOnly.FromDateTime(DateTime.Today),
            Payload = new CalculatorPayload
            {
                Leaves = new Dictionary<EmployeePayDateKey, List<LeaveApplicationPyRun>>
                {
                    [new EmployeePayDateKey(empId, DateOnly.FromDateTime(DateTime.Today))] = new List<LeaveApplicationPyRun> { leave1, leave2 }
                },
                LeaveCredits = LeaveCreditsHelper.CreateLeaveCredits(empId, new[]
                {
                    (leaveId1, 5m),
                    (leaveId2, 5m)
                })
            }
        };
    }

}
public static class LeaveHelper
{
    public static LeaveApplicationPyRun CreateLeave(Guid employeeId, Guid leaveId,
        LeaveDayType dayType = LeaveDayType.WholeDay, PayType payType = PayType.WithPay, DateOnly? leaveDate = null)
    {
        return new LeaveApplicationPyRun
        {
            LeaveId = leaveId,
            DayType = dayType,
            PayType = payType,
            EmployeeId = employeeId,
            LeaveDate = leaveDate ?? DateOnly.FromDateTime(DateTime.Today),
        };
    }
}
public static class LeaveCreditsHelper
{
    public static Dictionary<EmployeeLeaveCreditsKey, decimal> CreateLeaveCredits(
        Guid employeeId, IEnumerable<(Guid leaveId, decimal credits)> leaveCredits)
    {
        var dict = new Dictionary<EmployeeLeaveCreditsKey, decimal>();
        foreach (var (leaveId, credits) in leaveCredits)
        {
            dict[new EmployeeLeaveCreditsKey(employeeId, leaveId)] = credits;
        }
        return dict;
    }
}