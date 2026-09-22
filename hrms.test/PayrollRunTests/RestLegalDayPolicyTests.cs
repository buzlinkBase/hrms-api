using Hrms.Core.Policies.DTRPolicies;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Regression guard for the Rest+Legal Holiday unworked-pay gap: RestLegalDayPolicy computed the
/// correct blended Value all along, but never passed its BasicPipelineData `line` into
/// DayTypePayCalculator.For(...), so `.Worked`/`.UnWork` were never populated -- unlike
/// RegularHolidayPolicy (plain Legal Holiday), which already did. That made it impossible to
/// break "Rest Day + Legal Holiday" pay into a separate worked/unworked figure the way
/// Payroll.LegalHolidayUnworkedPay does for plain Legal Holiday. See Payroll.RestLegalUnworkedPay.
/// </summary>
public class RestLegalDayPolicyTests
{
    private const decimal HourlyRate = 100m;
    private const double ShiftHours = 8;

    private static PayrollContext CreateContext(double restLegalDayHours, WorkType workType)
    {
        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                DailyRate = HourlyRate * (decimal)ShiftHours,
                SalaryType = SalaryType.VARIABLE,
                Settings = new EmployeeSettingModel { IsEligibleForRegularHolidayPay = true },
            },
            DailyRecord = new DailyRecordRunModel
            {
                ShiftWorkingHour = ShiftHours,
                WorkTypeEnum = workType,
                RestLegalDayHours = restLegalDayHours,
            },
            Payload = new CalculatorPayload(),
        };
    }

    private static void SetCompanyRate(PayrollContext context, RateType type, decimal rate)
        => context.Payload.PremiumRates[type] = rate;

    [Fact]
    public void PartiallyWorked_LineNowReportsBothWorkedAndUnworkedAmounts()
    {
        // 4 of 8 hours actually worked on a day that's simultaneously a Rest Day and a Legal
        // Holiday -- before the fix, line.Worked/line.UnWork both stayed 0 regardless (the
        // `line` argument was silently dropped), even though the blended Value was correct.
        var context = CreateContext(restLegalDayHours: 4, WorkType.RestDayLegalHolidayDuty);
        SetCompanyRate(context, RateType.RESTDAY_DUTY, 1.30m);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);

        var line = new RestLegalDayPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        var expectedWorked = HourlyRate * 4 * (1.30m * 2.00m); // combo rate: RESTDAY_DUTY x LEGAL_HOLIDAY_DUTY
        var expectedUnworked = HourlyRate * 4 * 1.0m; // unworked-but-eligible: flat 1.0x, not the combo rate

        line.Worked.Should().Be(expectedWorked);
        line.UnWork.Should().Be(expectedUnworked);
        line.Value.Should().Be(expectedWorked + expectedUnworked); // blended total is unchanged by this fix
    }

    [Fact]
    public void FullyWorked_UnworkedIsZero_WorkedCarriesTheWholeAmount()
    {
        var context = CreateContext(restLegalDayHours: 8, WorkType.RestDayLegalHolidayDuty);
        SetCompanyRate(context, RateType.RESTDAY_DUTY, 1.30m);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);

        var line = new RestLegalDayPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        line.UnWork.Should().Be(0m);
        line.Worked.Should().Be(line.Value);
    }
}
