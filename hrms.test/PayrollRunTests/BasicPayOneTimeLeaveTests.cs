using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// PayrollProcessorService.CountOneTimeLeaveCalendarDays / GetBasicPay — a FIXED employee's
/// flat MonthlyRate/divisor implicitly pays for every calendar day in the period unless
/// explicitly deducted; OneTime-payout leave days (e.g. a Shared-funded SSS maternity lump
/// sum, paid via ApplyOneTimeLeavePayoutsToGross) generate no Late/UT/Absent/UnpaidLeave, so
/// without this deduction the flat rate would silently double-pay on top of the lump sum. See
/// GenerateLastPayAsync's plan notes / the maternity-leave bug fix.
/// </summary>
public class BasicPayOneTimeLeaveTests
{
    private static EmployeeModelPayrollRun FixedEmployee(decimal monthlyRate = 30_000, decimal dailyRate = 1_000) => new()
    {
        Id = Guid.NewGuid(),
        SalaryType = SalaryType.FIXED,
        MonthlyRate = monthlyRate,
        DailyRate = dailyRate,
        PayrollGroup = new PayrollGroupModel { PayrollFrequency = PayrollFrequency.SEMI_MONTHLY },
    };

    private static LeaveApplication OneTimeLeave(DateOnly from, DateOnly to, PayoutMode mode = PayoutMode.OneTime, PayType payType = PayType.WithPay) => new()
    {
        Leave = new Leave { Description = "Maternity Leave" },
        LeaveDateFrom = from,
        LeaveDateTo = to,
        PayoutMode = mode,
        PayType = payType,
    };

    // ── CountOneTimeLeaveCalendarDays ────────────────────────────────────────────────────

    [Fact]
    public void CountOneTimeLeaveCalendarDays_NoLeavesForEmployee_ReturnsZero()
    {
        var payload = new CalculatorPayload();
        PayrollProcessorService.CountOneTimeLeaveCalendarDays(payload, Guid.NewGuid(),
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15)).Should().Be(0);
    }

    [Fact]
    public void CountOneTimeLeaveCalendarDays_LeaveFullyInsidePeriod_CountsEveryCalendarDay()
    {
        var empId = Guid.NewGuid();
        var payload = new CalculatorPayload();
        payload.Leaves[new Leavekey(empId)] = new List<LeaveApplication>
        {
            OneTimeLeave(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 10)),
        };

        // 8 days inclusive (Aug 3–10), including whatever rest days fall inside — the flat
        // rate itself already implicitly covers rest days as part of the period share.
        PayrollProcessorService.CountOneTimeLeaveCalendarDays(payload, empId,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15)).Should().Be(8);
    }

    [Fact]
    public void CountOneTimeLeaveCalendarDays_LeaveSpansOutsidePeriod_ClipsToPeriodBoundaries()
    {
        var empId = Guid.NewGuid();
        var payload = new CalculatorPayload();
        payload.Leaves[new Leavekey(empId)] = new List<LeaveApplication>
        {
            // Maternity spans well past this one pay period — only the overlap counts.
            OneTimeLeave(new DateOnly(2026, 7, 20), new DateOnly(2026, 11, 1)),
        };

        PayrollProcessorService.CountOneTimeLeaveCalendarDays(payload, empId,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15)).Should().Be(15);
    }

    [Fact]
    public void CountOneTimeLeaveCalendarDays_PerDayPayoutMode_IsNotCounted()
    {
        var empId = Guid.NewGuid();
        var payload = new CalculatorPayload();
        payload.Leaves[new Leavekey(empId)] = new List<LeaveApplication>
        {
            OneTimeLeave(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 10), mode: PayoutMode.PerDay),
        };

        PayrollProcessorService.CountOneTimeLeaveCalendarDays(payload, empId,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15)).Should().Be(0);
    }

    [Fact]
    public void CountOneTimeLeaveCalendarDays_WithoutPay_IsNotCounted()
    {
        var empId = Guid.NewGuid();
        var payload = new CalculatorPayload();
        payload.Leaves[new Leavekey(empId)] = new List<LeaveApplication>
        {
            OneTimeLeave(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 10), payType: PayType.WithoutPay),
        };

        PayrollProcessorService.CountOneTimeLeaveCalendarDays(payload, empId,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15)).Should().Be(0);
    }

    [Fact]
    public void CountOneTimeLeaveCalendarDays_MultipleApplications_Sum()
    {
        var empId = Guid.NewGuid();
        var payload = new CalculatorPayload();
        payload.Leaves[new Leavekey(empId)] = new List<LeaveApplication>
        {
            OneTimeLeave(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2)), // 2 days
            OneTimeLeave(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12)), // 3 days
        };

        PayrollProcessorService.CountOneTimeLeaveCalendarDays(payload, empId,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15)).Should().Be(5);
    }

    // ── GetBasicPay ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetBasicPay_Fixed_NoOneTimeLeave_UnchangedFromExistingDeductions()
    {
        var employee = FixedEmployee(monthlyRate: 30_000);
        var line = new PayrollSummaryLine { PayPeriodStart = new DateOnly(2026, 8, 1) };
        var timeCalc = new List<DTRPayModel> { new() { LateAmount = 100, UTAmount = 50 } };

        PayrollProcessorService.GetBasicPay(line, timeCalc, employee, oneTimeLeaveDays: 0);

        // SEMI_MONTHLY divisor is 2: 30,000/2 - 150 = 14,850
        line.BasicPay.Should().Be(14_850);
    }

    [Fact]
    public void GetBasicPay_Fixed_OneTimeLeaveDays_DeductsDailyRateEquivalent_OnTopOfExistingDeductions()
    {
        var employee = FixedEmployee(monthlyRate: 30_000, dailyRate: 1_000);
        var line = new PayrollSummaryLine { PayPeriodStart = new DateOnly(2026, 8, 1) };
        var timeCalc = new List<DTRPayModel>();

        PayrollProcessorService.GetBasicPay(line, timeCalc, employee, oneTimeLeaveDays: 8);

        // 30,000/2 - (8 * 1,000) = 7,000 — the flat rate no longer double-pays the 8
        // maternity-leave days already compensated via the OneTime lump sum.
        line.BasicPay.Should().Be(7_000);
    }

    [Fact]
    public void GetBasicPay_Fixed_OneTimeLeaveDeduction_AppliesRegardlessOfPaySource()
    {
        // GetBasicPay has no PaySource awareness at all — the flat rate implicitly covers
        // every calendar day not explicitly deducted, so the deduction must apply the same
        // way whether the leave is Government-only, Shared, or Company-funded.
        var employee = FixedEmployee(monthlyRate: 30_000, dailyRate: 1_000);
        var line = new PayrollSummaryLine { PayPeriodStart = new DateOnly(2026, 8, 1) };

        PayrollProcessorService.GetBasicPay(line, new List<DTRPayModel>(), employee, oneTimeLeaveDays: 5);

        line.BasicPay.Should().Be(30_000m / 2 - 5_000);
    }

    [Fact]
    public void GetBasicPay_Variable_IsUnaffectedByOneTimeLeaveDays_UsesRegularDayPayOnly()
    {
        var employee = new EmployeeModelPayrollRun { Id = Guid.NewGuid(), SalaryType = SalaryType.VARIABLE };
        var line = new PayrollSummaryLine();
        var timeCalc = new List<DTRPayModel> { new() { RegularDayPay = 4_000 }, new() { RegularDayPay = 3_500 } };

        // Passing a nonzero oneTimeLeaveDays should have no effect on VARIABLE — its branch
        // returns before that term is ever used. Once IsEligibleForVirtualAttendance excludes
        // OneTime-leave days from virtual attendance, those days simply never contribute
        // RegularDayPay in the first place.
        PayrollProcessorService.GetBasicPay(line, timeCalc, employee, oneTimeLeaveDays: 8);

        line.BasicPay.Should().Be(7_500);
    }
}
