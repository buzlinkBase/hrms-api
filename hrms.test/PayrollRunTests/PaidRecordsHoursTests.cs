using Hrms.Api.Documents;
using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Payslip hours x rate must match the amounts. Reported case (JDA, Sep 16-30 2026, daily rate
/// 540): 14 paid 12-hour days, but the payslip showed 120 regular / 60 OT / 56 ND hours against
/// pay for 112 / 56 / 52 -- one extra DTR row (4 net + 4 ND + 4 OT) was summed into the hours by
/// ComputeHoursBreakdown but never paid, because pay only takes one row per date in the cutoff
/// (SelectDtrRecordForDate). Hours now come from the same paid rows (SelectPaidRecords), and the
/// payslip's Regular row shows the paid RegularDayPay rather than hours x hourly.
/// </summary>
public class PaidRecordsHoursTests
{
    private static readonly Guid EmployeeId = Guid.NewGuid();
    private static readonly DateOnly From = new(2026, 9, 16);
    private static readonly DateOnly To = new(2026, 9, 30);

    // A guard's 12-hour day: 8 regular + 4 OT, all OT at night. Hours are natural, so OT and
    // ND each include the 4 NDOT hours.
    private static DailyRecordRunModel GuardDay(DateOnly date, string batch = "B1") => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = EmployeeId,
        WorkDate = date,
        BatchCode = batch,
        RegularNetHours = 8,
        RegularOTHours = 4,
        RegularNDHours = 4,
        RegularNDOTHours = 4,
    };

    // The stray row's shape from the reported payslip.
    private static DailyRecordRunModel StrayRow(DateOnly date) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = EmployeeId,
        WorkDate = date,
        BatchCode = "OLD",
        RegularNetHours = 8,
        RegularNDHours = 4,
        RegularOTHours = 4,
    };

    private static List<DailyRecordRunModel> FourteenDays() =>
        Enumerable.Range(0, 14).Select(i => GuardDay(From.AddDays(i))).ToList();

    private static PayrollSummaryLine HoursOf(List<DailyRecordRunModel> dtrs)
    {
        var line = new PayrollSummaryLine();
        EmployeePayrollLineService.ComputeHoursBreakdown(
            EmployeePayrollLineService.SelectPaidRecords(dtrs, EmployeeId, From, To), line);
        return line;
    }

    [Fact]
    public void ASecondRowForAPaidDate_IsNotCountedInTheHours()
    {
        var dtrs = FourteenDays();
        dtrs.Add(StrayRow(From.AddDays(3)));

        var line = HoursOf(dtrs);

        line.RegularNetHours.Should().Be(112);
        line.RegularNDHours.Should().Be(56);
        line.RegularOTHours.Should().Be(56);
        line.RegularNDOTHours.Should().Be(56);
    }

    [Fact]
    public void ARowOutsideTheCutoff_IsNotCountedInTheHours()
    {
        var dtrs = FourteenDays();
        dtrs.Add(StrayRow(From.AddDays(-1)));
        dtrs.Add(StrayRow(To.AddDays(1)));

        HoursOf(dtrs).RegularNetHours.Should().Be(112);
    }

    [Fact]
    public void OneRowPerDate_InTheCutoff_IsWhatGetsPaid()
    {
        var dtrs = FourteenDays();
        dtrs.Add(StrayRow(From.AddDays(3)));
        dtrs.Add(StrayRow(To.AddDays(1)));

        var paid = EmployeePayrollLineService.SelectPaidRecords(dtrs, EmployeeId, From, To);

        paid.Should().HaveCount(14);
        paid.Should().OnlyContain(r => r.BatchCode == "B1");
    }

    private static List<PayrollDtrDetail> PaidDays(int count, decimal regularDayPay) =>
        Enumerable.Range(0, count).Select(_ => new PayrollDtrDetail { RegularDayPay = regularDayPay }).ToList();

    [Fact]
    public void ReportedPayslip_RowsMatchThePay_AndAddUpToGross()
    {
        const decimal hourly = 67.5m;
        var payroll = new Payroll
        {
            DailyRate = 540m,
            SalaryType = SalaryType.VARIABLE,
            OtNdCalculationMethod = OtNdCalculationMethod.Additive,
            RegularNetHours = 112,
            BasicPay = 112 * hourly, // 7,560.00
            TimeHourPayResults = PaidDays(14, 8 * hourly),
            RegularNDOTHours = 52,
            RegularNDOTBasePay = 52 * hourly * 1.25m, // 4,387.50
            RegularOTHours = 56, // natural: 4 daytime + 52 NDOT
            RegularNDHours = 52, // natural: the 52 NDOT hours
            RegularOTBasePay = 4 * hourly * 1.25m, // 337.50
            RegularNDOTPremiumPay = 52 * hourly * 0.10m, // 351.00
            GrossIncome = 12636m,
        };

        var rows = new PayslipHoursDocument(payroll, new EmployeeFullModel()).BuildRows();

        var regular = rows.Single(r => r.Label == "Regular Hours");
        regular.Hours.Should().Be(112);
        regular.Amount.Should().Be(7560m);
        var ot = rows.Single(r => r.Label == "Regular OT Hours");
        ot.Hours.Should().Be(56);
        ot.Amount.Should().Be(4725m);
        var nd = rows.Single(r => r.Label == "Night Diff.");
        nd.Hours.Should().Be(52);
        nd.Amount.Should().Be(351m);
        rows.Where(r => !r.IsGroupHeader).Sum(r => r.Amount).Should().Be(payroll.GrossIncome);
    }

    // Even if the stored hours ever drift from the pay again, the Regular row's amount is the
    // paid figure, never hours x hourly.
    [Fact]
    public void RegularRow_ShowsThePaidAmount_NotHoursTimesRate()
    {
        var payroll = new Payroll
        {
            DailyRate = 540m,
            SalaryType = SalaryType.VARIABLE,
            RegularNetHours = 116,
            TimeHourPayResults = PaidDays(14, 540m),
        };

        new PayslipHoursDocument(payroll, new EmployeeFullModel()).BuildRows()
            .Single(r => r.Label == "Regular Hours").Amount.Should().Be(7560m);
    }
}
