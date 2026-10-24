using Hrms.Api.Documents.Shared;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

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

    private static PayrollBreakdownItem Item(string label, decimal amount, bool isLoan) =>
        new(Guid.NewGuid(), label, amount, isLoan);

    [Fact]
    public void DeductionRows_ItemizesLoansThenOthers_WhenItemsCoverTheTotals()
    {
        var payroll = new Payroll
        {
            TotalLoans = 1500m,
            OtherDeductions = 1800m,
            DeductionItems =
            [
                Item("Union Dues", 300m, false),
                Item("SSS Loan", 1000m, true),
                Item("Pag-IBIG Loan", 500m, true),
            ],
        };

        PayslipSections.DeductionRows(payroll).Should().Equal(
            ("SSS Loan", 1000m), ("Pag-IBIG Loan", 500m), ("Union Dues", 300m));
    }

    [Fact]
    public void DeductionRows_AddsOneLineForWhatTheItemsDontCover()
    {
        // 200 of Other Deductions is a Salary Adjustment: no installment row, so no item.
        var payroll = new Payroll
        {
            TotalLoans = 1000m,
            OtherDeductions = 1500m,
            DeductionItems = [Item("SSS Loan", 1000m, true), Item("Union Dues", 300m, false)],
        };

        PayslipSections.DeductionRows(payroll).Should().Equal(
            ("SSS Loan", 1000m), ("Union Dues", 300m), ("Other Deductions", 200m));
    }

    [Fact]
    public void DeductionRows_FallsBackToTheTotals_WhenThereAreNoItems()
    {
        var payroll = new Payroll { TotalLoans = 1000m, OtherDeductions = 1300m };

        PayslipSections.DeductionRows(payroll).Should().Equal(("Loans", 1000m), ("Other Deductions", 300m));
    }

    [Fact]
    public void NoDtrRows_ReturnsZero()
    {
        var payroll = new Payroll();

        PayslipSections.DaysWorked(payroll).Should().Be(0);
    }
}
