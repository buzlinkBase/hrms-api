using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using Xunit;

namespace hrms.test;

public class RegularHolidayPipeLineTest
{

    [Fact]
    public void ApplyIfSatisfied_PureLegalHoliday_ComputesDoubleDailyRate()
    {
        var context = RegularHolidayTestHelpers.CreatePayrollContextWithRegularHoliday(dailyRate: 1000, regularHolidayHours: 8);
        var pipe = new RegularHolidayPipeLine();
        var result = pipe.Run(context);
        Assert.Equal(2000, result.Value); // 1 day × 1000 × 2.0
        Assert.Equal(WorkType.RegularHolidayDuty, context.WorkType);
    }


    [Fact]
    public void ApplyIfSatisfied_PureLegalHoliday_ComputesDoubleDailyRateWithCredit()
    {
        var context = RegularHolidayTestHelpers.CreatePayrollContextWithRegularHoliday(dailyRate: 1000, regularHolidayHours: 15);
        context.Payload.CompanyPolicy.HolidayCreditPolicy = HolidayCreditMode.AutoCredit;
        var pipe = new RegularHolidayPipeLine();
        var result = pipe.Run(context);
        Assert.Equal(1875, result.Value);
    }

    [Fact]
    public void ApplyIfSatisfied_PureLegalHoliday_NotWorkingFull8hr()
    {
        var context = RegularHolidayTestHelpers.CreatePayrollContextWithRegularHoliday(dailyRate: 1000, regularHolidayHours: 7);
        context.DailyRecord.HolCount = 1;
        var pipe = new RegularHolidayPipeLine();
        var result = pipe.Run(context);
        Assert.Equal(1875, result.Value); // 15hrs × 125
    }

    [Fact]
    public void ApplyIfSatisfied_RestDayLegalHoliday_ComputesRestDayRate()
    {
        var context = RegularHolidayTestHelpers.CreatePayrollContextWithRegularHoliday(dailyRate: 1000, regularHolidayHours: 8);
        context.WorkType = WorkType.RestDayLegalHolidayDuty;
        var pipe = new RegularHolidayPipeLine();
        var result = pipe.Run(context);
        Assert.Equal(2600, result.Value); // 1 day × 1000 × 2.6
    }

    [Fact]
    public void ApplyIfSatisfied_RestDayLegalHoliday_ComputesRestDayNonFull8()
    {
        var context = RegularHolidayTestHelpers.CreatePayrollContextWithRegularHoliday(dailyRate: 1000, regularHolidayHours: 7);
        context.WorkType = WorkType.RestDayLegalHolidayDuty;
        var pipe = new RegularHolidayPipeLine();
        var result = pipe.Run(context);
        Assert.Equal(2400m, result.Value);
    }


    [Fact]
    public void ApplyIfSatisfied_NoRuleMatch_DefaultRateApplied()
    {
        var context = RegularHolidayTestHelpers.CreatePayrollContextWithRegularHoliday(dailyRate: 1000, regularHolidayHours: 8);
        context.DailyRecord.HolCount = 0;
        context.WorkType = WorkType.RegularHoliday;
        var pipe = new RegularHolidayPipeLine();
        var result = pipe.Run(context);
        Assert.Equal(1000, result.Value);
    }
    [Fact]
    public void ApplyIfSatisfied_FractionalHours_ComputesProRatedValue()
    {
        var context = RegularHolidayTestHelpers.CreatePayrollContextWithRegularHoliday(dailyRate: 1000, regularHolidayHours: 4);
        var pipe = new RegularHolidayPipeLine();
        var result = pipe.Run(context);
        Assert.Equal(1500, result.Value); // 1000 + 0.5 day × 1000 
    }
    [Fact]
    public void Run_ShouldApplyLegalHoliday_WhenNoWork_NotEligible()
    {
        var context = RegularHolidayTestHelpers.CreatePayrollContextWithRegularHoliday(
            dailyRate: 1000,
            regularHolidayHours: 0
        );
        context.DailyRecord.HolCount = 0;//not eligible
        context.WorkType = WorkType.RegularHoliday;
        var pipeline = new RegularHolidayPipeLine();
        var expected = 0;
        var result = pipeline.Run(context);
        Assert.Equal(expected, result.Value);
    }
    [Fact]
    public void Run_ShouldApplyLegalHoliday_WhenWorkingNotEligible()
    {
        var context = RegularHolidayTestHelpers.CreatePayrollContextWithRegularHoliday(
            dailyRate: 1000,
            regularHolidayHours: 8
        );
        context.DailyRecord.HolCount = 0;//not eligible
        context.WorkType = WorkType.RegularHoliday;
        var pipeline = new RegularHolidayPipeLine();
        var expected = 1000;
        var result = pipeline.Run(context);
        Assert.Equal(expected, result.Value);
    }
    [Fact]
    public void Run_ShouldApplyRestDayLegalHolidayDuty()
    {
        var context = RegularHolidayTestHelpers.CreatePayrollContextWithRegularHoliday(
            dailyRate: 1000,
            regularHolidayHours: 8
        );
        context.WorkType = WorkType.RestDayLegalHolidayDuty;
        var pipeline = new RegularHolidayPipeLine();

        var expected = 2.6m * 1000m;//2600
        var result = pipeline.Run(context);

        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Run_ShouldApplyRestDayLegalHolidayDutyHalfWork()
    {
        var context = RegularHolidayTestHelpers.CreatePayrollContextWithRegularHoliday(
            dailyRate: 1000,
            regularHolidayHours: 4
        );
        context.WorkType = WorkType.RestDayLegalHolidayDuty;
        var pipeline = new RegularHolidayPipeLine();

        //1.6 instead of 2.6 since 1000 is added separately
        var expected = 1000 + (1.6m * 1000m / 8 * 4m); //1800
        var result = pipeline.Run(context);
        Assert.Equal(expected, result.Value);

    }
}

public static class RegularHolidayTestHelpers
{
    public static PayrollContext CreatePayrollContextWithRegularHoliday(
        decimal dailyRate = 1000,
        int regularHolidayHours = 0)
    {
        var empId = Guid.NewGuid();

        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                Id = empId,
                DailyRate = dailyRate,
                Settings = new EmployeeSettingModel { IsEligibleForHolidayPay = true }
            },
            PayrollDate = DateOnly.FromDateTime(DateTime.Today),
            Payload = new CalculatorPayload
            {
                Leaves = new Dictionary<EmployeePayDateKey, List<LeaveApplicationPyRun>>(),
                LeaveCredits = new Dictionary<EmployeeLeaveCreditsKey, decimal>(),
                CompanyPolicy = new CompanyPolicyRule { HolidayCreditPolicy = HolidayCreditMode.NoCredit },
                PremiumRates = TestRateProvider.GetDefaultPremiumRates(),
            },
            DailyRecord = new DailyRecordRunModel
            {
                LegalHolHours = regularHolidayHours,
                HolCount = 1,
            },
            WorkType = WorkType.RegularHolidayDuty
        };
    }
}