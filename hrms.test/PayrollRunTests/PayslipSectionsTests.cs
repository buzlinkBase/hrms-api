using Hrms.Api.Documents.Shared;
using Hrms.Domain.Entities;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// PayslipSections.DaysWorked -- counts distinct DTR dates with any pay > 0 across every
/// day-type/tier, sourced from Payroll.TimeHourPayResults. Added alongside the payslip's new
/// "Days Worked" field.
/// </summary>
public class PayslipSectionsTests
{
    private static PayrollDtrDetail Day(DateOnly date, decimal regularDayPay = 0m, decimal restDayOtPay = 0m) => new()
    {
        Date = date,
        RegularDayPay = regularDayPay,
        RestDayOTPay = restDayOtPay,
    };

    [Fact]
    public void CountsOnlyDistinctDatesWithSomePayAcrossAnyCategoryOrTier()
    {
        var payroll = new Payroll
        {
            TimeHourPayResults =
            {
                Day(new DateOnly(2026, 9, 1), regularDayPay: 540m),
                Day(new DateOnly(2026, 9, 2), regularDayPay: 540m),
                // Zero pay everywhere -- an absence or fully-unworked day -- must not count.
                Day(new DateOnly(2026, 9, 3)),
                // Pay only in a non-Regular tier (e.g. a rest-day OT call-in) still counts.
                Day(new DateOnly(2026, 9, 4), restDayOtPay: 300m),
            },
        };

        PayslipSections.DaysWorked(payroll).Should().Be(3);
    }

    [Fact]
    public void NoDtrRows_ReturnsZero()
    {
        var payroll = new Payroll();

        PayslipSections.DaysWorked(payroll).Should().Be(0);
    }
}
