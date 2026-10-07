using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Providers;

/// <summary>
/// AttendanceProvider.CurrentShiftAttendance — the logs the DTR captures for a shift come from
/// AttendanceCaptureWindow: from the earliest allowed clock-in (CompanyPolicy.TimeInAllowance,
/// default -120) up to, not including, the next shift's earliest allowed clock-in. Split shifts
/// get no allowance. Shift is 2026-09-19 12:00-20:00; DtrTestBase seeds the next shift at the
/// same time the following day (2026-09-20 12:00).
/// </summary>
public class AttendanceProviderTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 9, 19, 12, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 9, 19, 20, 0, 0);
    private static readonly DateTime NextShiftStart = new(2026, 9, 20, 12, 0, 0);

    private static List<DateTime> Captured(TimeContext context) =>
        context.Payload.Provider.AttendanceProvider.CurrentShiftAttendance()
            .Select(x => x.WorkDateTime)
            .OrderBy(x => x)
            .ToList();

    [Fact]
    public void EarlyClockInWithinTheAllowance_IsCaptured_UpToTheNextShiftsEarliestClockIn()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        var earlyIn = ShiftStart.AddMinutes(-90); // 10:30
        var lateOut = new DateTime(2026, 9, 20, 0, 7, 37);
        ApplyAttendance(context,
            ShiftStart.AddMinutes(-121), // 09:59, before the earliest allowed clock-in
            earlyIn,
            lateOut,
            NextShiftStart.AddMinutes(-120)); // the next shift's earliest allowed clock-in

        Captured(context).Should().Equal(earlyIn, lateOut);
    }

    [Fact]
    public void CompanyTimeInAllowance_SetsTheEarliestCapturedClockIn()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        context.Payload.Data.CompanyPolicy.TimeInAllowance = -30;
        ApplyAttendance(context, ShiftStart.AddMinutes(-31), ShiftStart.AddMinutes(-30));

        Captured(context).Should().Equal(ShiftStart.AddMinutes(-30));
    }

    [Fact]
    public void SplitShift_HasNoClockInAllowance()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        context.Payload.Data.CurrentShift.ShiftType = TimeShiftType.SPLIT;
        ApplyAttendance(context, ShiftStart.AddMinutes(-1), ShiftStart, NextShiftStart.AddMinutes(-1), NextShiftStart);

        Captured(context).Should().Equal(ShiftStart, NextShiftStart.AddMinutes(-1));
    }
}
