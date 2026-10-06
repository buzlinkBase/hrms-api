using Hrms.Core.Policies.DTRPolicies;
using CompanyPolicyService = DTR.Core.CompanyPolicyService;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Setup > Company Policy > Hourly Rate Divisor -- what DailyRate is divided by to get the hourly
/// rate every payroll hour bucket is priced from (RateHelper.GetHourlyRate):
/// - Fixed8Hours (default): always 8, regardless of the shift length.
/// - ShiftMaxWorkingHours: the day's shift length.
/// </summary>
public class HourlyRateDivisorTests
{
    private const decimal DailyRate = 720m;

    private static PayrollContext CreateContext(HourlyRateDivisor divisor, double shiftHours, double lateMinutes = 0)
    {
        var context = new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun { DailyRate = DailyRate },
            DailyRecord = new DailyRecordRunModel { ShiftWorkingHour = shiftHours, LateMinutes = lateMinutes },
            Payload = new CalculatorPayload(),
        };
        context.Payload.CompanyPolicy.HourlyRateDivisor = divisor;
        return context;
    }

    [Theory]
    [InlineData(8, 90)]
    [InlineData(9, 80)]
    [InlineData(10, 72)]
    public void ShiftMaxWorkingHours_DividesByTheDaysShiftLength(double shiftHours, decimal expectedHourly)
    {
        var context = CreateContext(HourlyRateDivisor.ShiftMaxWorkingHours, shiftHours);

        RateHelper.GetHourlyRate(context).Should().Be(expectedHourly);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Fixed8Hours_AlwaysDividesByEight(double shiftHours)
    {
        var context = CreateContext(HourlyRateDivisor.Fixed8Hours, shiftHours);

        RateHelper.GetHourlyRate(context).Should().Be(90m);
    }

    [Fact]
    public void UnknownDivisor_Throws()
    {
        var context = CreateContext((HourlyRateDivisor)99, 8);

        var act = () => RateHelper.GetHourlyRate(context);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(HourlyRateDivisor.ShiftMaxWorkingHours, 80)] // 720 / 9 = 80/hr x 1.5h
    [InlineData(HourlyRateDivisor.Fixed8Hours, 90)]          // 720 / 8 = 90/hr x 1.5h
    public void LatePolicy_PricesThroughTheSelectedDivisor(HourlyRateDivisor divisor, decimal expectedHourly)
    {
        var context = CreateContext(divisor, shiftHours: 9, lateMinutes: 90);

        var line = new LatePolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        line.Value.Should().Be(expectedHourly * 1.5m);
    }

    [Fact]
    public void Default_IsFixed8Hours_ForEveryFallback()
    {
        HourlyRateDivisorDefaults.Default.Should().Be(HourlyRateDivisor.Fixed8Hours);
        new CalculatorPayload().CompanyPolicy.HourlyRateDivisor.Should().Be(HourlyRateDivisor.Fixed8Hours);
        new CompanyPolicyService().Transform(new Dictionary<string, GeneralSettingModel>())
            .HourlyRateDivisor.Should().Be(HourlyRateDivisor.Fixed8Hours);
    }

    [Fact]
    public void Default_PricesA9HourShiftAtDailyRateOver8()
    {
        var context = new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun { DailyRate = DailyRate },
            DailyRecord = new DailyRecordRunModel { ShiftWorkingHour = 9 },
            Payload = new CalculatorPayload(),
        };

        RateHelper.GetHourlyRate(context).Should().Be(90m);
    }

    [Theory]
    [InlineData("Fixed8Hours", HourlyRateDivisor.Fixed8Hours)]
    [InlineData("ShiftMaxWorkingHours", HourlyRateDivisor.ShiftMaxWorkingHours)]
    [InlineData("garbage", HourlyRateDivisor.Fixed8Hours)]
    public void CompanyPolicyService_ReadsTheSavedSetting(string saved, HourlyRateDivisor expected)
    {
        var key = SettingKey.HourlyRateDivisor.ToString();
        var settings = new Dictionary<string, GeneralSettingModel>
        {
            [key] = new() { Key = key, Value = saved },
        };

        new CompanyPolicyService().Transform(settings).HourlyRateDivisor.Should().Be(expected);
    }
}
