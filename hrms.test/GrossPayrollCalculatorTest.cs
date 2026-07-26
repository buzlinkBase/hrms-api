using Hrms.Domain;
using Hrms.Domain.ValueObjects;
using Xunit;

namespace hrms.test;

public class GrossPayrollCalculatorTest
{

    [Fact]
    public void ShouldCalculateRegularGross_WhenNDFallOnRegular()
    {
        var calculator = new BasicPayrollCalculator();
        var context = GrossTestHelpers.Create();

        context.DailyRecord = new DailyRecordRunModel
        {
            RegularNetHours = 8,
            RegularOTHours = 2,
            RegularNDHours = 1,
        };
        context.WorkType = WorkType.RegularWorkDay;
        var result = calculator.Calculate(context);

        Assert.Equal(1000, result.Basic);
        Assert.Equal(312.5m, result.OTHourInfo.Amount);
        Assert.Equal(12.5m, result.NightDiffInfo.Amount);
        Assert.Equal(2, result.OTHourInfo.Hour);
        Assert.Equal(1, result.NightDiffInfo.Hour);
        Assert.Equal(1325m, result.TimeBaseGross);
    }

    [Fact]
    public void ShouldCalculateRegularGross_WhenNDFallOnOT()
    {
        var calculator = new BasicPayrollCalculator();
        var context = GrossTestHelpers.Create();

        context.DailyRecord = new DailyRecordRunModel
        {
            RegularNetHours = 8,
            RegularOTHours = 2,
            RegularNDOTHours = 1,
        };
        context.WorkType = WorkType.RegularWorkDay;
        var result = calculator.Calculate(context);

        Assert.Equal(1000, result.Basic);
        Assert.Equal(312.5m, result.OTHourInfo.Amount);
        Assert.Equal(12.5m, result.NightDiffInfo.Amount); // ND premium only
        Assert.Equal(2, result.OTHourInfo.Hour);
        Assert.Equal(1, result.NightDiffInfo.Hour);
        Assert.Equal(1325m, result.TimeBaseGross);
    }

    [Fact]
    public void ShouldCalculateRestDayGross_WhenNDFallOnRestDay()
    {
        var calculator = new BasicPayrollCalculator();
        var context = GrossTestHelpers.Create();
        context.DailyRecord = new DailyRecordRunModel
        {
            RestDayHours = 8,
            RestDayOTHours = 2,
            RestDayNDHours = 1,
        };

        context.WorkType = WorkType.RestDayDuty;
        var result = calculator.Calculate(context);

        Assert.Equal(0, result.Basic);
        Assert.Equal(1300m, result.RestDayDuty);
        Assert.Equal(406.25m, result.OTHourInfo.Amount);
        Assert.Equal(12.5m, result.NightDiffInfo.Amount); // ND premium only
        Assert.Equal(2, result.OTHourInfo.Hour);
        Assert.Equal(1, result.NightDiffInfo.Hour);
        Assert.Equal(1718.75m, result.TimeBaseGross);
    }
}

public static class GrossTestHelpers
{
    public static PayrollContext Create()
    {
        var empId = Guid.NewGuid();

        var pydate = DateOnly.FromDateTime(DateTime.Today);
        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                Id = empId,
                DailyRate = 1000,
                MonthlyRate = 3000,
                Settings = new EmployeeSettingModel { IsEligibleForNightDifferential = true, IsEligibleForOvertime = true },
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