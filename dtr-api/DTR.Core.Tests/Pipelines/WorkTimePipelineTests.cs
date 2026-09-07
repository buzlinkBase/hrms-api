using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Pipelines;

/// <summary>
/// WorkTimePipeline — the real attendance-computation chain (RegularHourPolicy ->
/// First8HrPolicy -> LatePolicy -> HolidayPolicy(LEGAL) -> HolidayPolicy(SPECIAL)), run
/// end to end with no ledger seeding or shortcuts. Covers the chain's own baseline behavior
/// (clean shift, unpaid break exclusion, late detection) plus confirms it's the real source
/// of the holiday_portion_{type}/non_holiday_portion ledger split that
/// NonHolidayDutyTimePipelineTests/HolidayDutyTimePipelineTests previously only exercised via
/// pre-seeded ledger state — see DtrRowIntegrationTests for the full, unshortened row-level
/// proof of the HolidayTimeBasis fix.
///
/// Deliberately out of scope: OvertimeHandlerProcessor's auto/approval-based OT computation
/// and WholeDayLateHandler/HalfDayLateHandler's actual truncation math (both gated behind
/// CompanyPolicy flags that default off, and OT additionally requires OTProvider to be wired)
/// — those are their own, larger attendance-policy subsystems independent of the
/// HolidayTimeBasis split this project exists to cover.
/// </summary>
public class WorkTimePipelineTests : DtrTestBase
{
    private static TimeRange Run(TimeContext context) =>
        new WorkTimePipeline(context, new IsEligibleWorkHours()).Apply(context.CanonicalTimeRange);

    [Fact]
    public void CleanEightHourShift_NoBreaksNoLateNoHoliday_PassesThroughUnchanged()
    {
        var context = CreateContext(
            new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 16, 0, 0));

        var result = Run(context);

        result.TotalMinutes.Should().Be(480);
    }

    [Fact]
    public void UnpaidLunchBreak_IsExcludedFromTheReturnedTime()
    {
        var shiftStart = new DateTime(2026, 1, 1, 8, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 1, 17, 0, 0); // 9-hour window
        var context = CreateContext(shiftStart, shiftEnd, maxWorkingMinutes: 480); // 8h net of the 1h lunch
        context.Payload.Data.CurrentShift.LunchBreakOption = BreakMode.UNPAID_BREAK;
        context.Payload.Data.CurrentShift.LunchStartTime = new DateTime(2026, 1, 1, 12, 0, 0);
        context.Payload.Data.CurrentShift.LunchEndTime = new DateTime(2026, 1, 1, 13, 0, 0);

        var result = Run(context);

        result.TotalMinutes.Should().Be(480); // 9h attendance - 1h unpaid lunch
    }

    [Fact]
    public void PaidLunchBreak_OneHour_BridgesThePunchGapAndCountsAsWorkedTime()
    {
        // Unlike UNPAID_BREAK (which subtracts a configured window from otherwise-continuous
        // attendance), PAID_BREAK only has an effect when there's an actual punch-out/punch-in
        // GAP in attendance at the lunch window (LunchBreakExtractor operates on gaps BETWEEN
        // records, see BreakExtractorsTests) -- ComputeUsableTime then adds that gap back in
        // (capped to LunchBreakDurationMinutes), bridging it so the employee is paid straight
        // through lunch despite having physically clocked out for it.
        var shiftStart = new DateTime(2026, 1, 1, 8, 0, 0);
        var lunchStart = new DateTime(2026, 1, 1, 12, 0, 0);
        var lunchEnd = new DateTime(2026, 1, 1, 13, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 1, 17, 0, 0); // 9-hour window, all of it paid
        var context = CreateContext(shiftStart, shiftEnd, maxWorkingMinutes: 540);
        context.Payload.Data.CurrentShift.LunchBreakOption = BreakMode.PAID_BREAK;
        context.Payload.Data.CurrentShift.LunchStartTime = lunchStart;
        context.Payload.Data.CurrentShift.LunchEndTime = lunchEnd;
        context.Payload.Data.CurrentShift.LunchBreakDurationMinutes = 60;
        // Attendance has a genuine gap at lunch -- the employee actually clocked out and back in.
        context.CanonicalTimeRange = TimeRange.Set(new TimeRecordCollection
        {
            new TimeRecord(shiftStart, lunchStart),
            new TimeRecord(lunchEnd, shiftEnd),
        });

        var result = Run(context);

        result.TotalMinutes.Should().Be(540); // full 9h -- the 1h lunch gap is paid, not deducted
        context.Payload.Ledger.GetByTag("paidBreak", context).TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void UnpaidVsPaidLunchBreak_SameNineHourWindow_YieldDifferentPaidTotals()
    {
        // Same shift boundaries, same 1h lunch window -- only LunchBreakOption differs -- to
        // make the pay-impact of the two settings directly comparable side by side.
        var shiftStart = new DateTime(2026, 1, 1, 8, 0, 0);
        var lunchStart = new DateTime(2026, 1, 1, 12, 0, 0);
        var lunchEnd = new DateTime(2026, 1, 1, 13, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 1, 17, 0, 0);

        var unpaidContext = CreateContext(shiftStart, shiftEnd, maxWorkingMinutes: 480);
        unpaidContext.Payload.Data.CurrentShift.LunchBreakOption = BreakMode.UNPAID_BREAK;
        unpaidContext.Payload.Data.CurrentShift.LunchStartTime = lunchStart;
        unpaidContext.Payload.Data.CurrentShift.LunchEndTime = lunchEnd;
        // Continuous attendance -- ExcludeBreakTime subtracts the configured window itself.
        unpaidContext.CanonicalTimeRange = Range(shiftStart, shiftEnd);

        var paidContext = CreateContext(shiftStart, shiftEnd, maxWorkingMinutes: 540);
        paidContext.Payload.Data.CurrentShift.LunchBreakOption = BreakMode.PAID_BREAK;
        paidContext.Payload.Data.CurrentShift.LunchStartTime = lunchStart;
        paidContext.Payload.Data.CurrentShift.LunchEndTime = lunchEnd;
        paidContext.Payload.Data.CurrentShift.LunchBreakDurationMinutes = 60;
        paidContext.CanonicalTimeRange = TimeRange.Set(new TimeRecordCollection
        {
            new TimeRecord(shiftStart, lunchStart),
            new TimeRecord(lunchEnd, shiftEnd),
        });

        Run(unpaidContext).TotalMinutes.Should().Be(480);
        Run(paidContext).TotalMinutes.Should().Be(540);
    }

    [Fact]
    public void LateClockIn_IsRecordedOnTheLedger_ButNotDeductedByDefault()
    {
        // WholeDayLateOn/HalfDayLateOn both default to false (CompanyPolicyRule), so a late
        // clock-in is detected and recorded but doesn't truncate regular hours by default —
        // that deduction is a separate, explicit company policy opt-in.
        var shiftStart = new DateTime(2026, 1, 1, 8, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 1, 16, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd);
        // Employee actually clocked in 30 minutes late and worked 30 minutes past shift end —
        // CapAndCrop clips the trailing 30 minutes back to the shift window, netting the same
        // total minutes actually attended within the shift (7.5h), not deducted further.
        context.CanonicalTimeRange = Range(shiftStart.AddMinutes(30), shiftEnd.AddMinutes(30));

        var result = Run(context);

        result.TotalMinutes.Should().Be(450); // 8h - 30 min late arrival, no further deduction
        context.Payload.Ledger.GetByTag("late", context).TotalMinutes.Should().Be(30);
    }

    // --- Non-standard shift lengths (compressed day, compressed workweek, extended hours) -----
    // MaxWorkingMinutes/the shift's own span drive everything here (TimeAllocationFactory ->
    // FixShiftAllocation.GetMaximumMinutes() just returns shift.MaxWorkingMinutes directly) --
    // these confirm WorkTimePipeline honors whatever the shift is configured for instead of
    // assuming a standard 8-hour/480-minute day anywhere in the chain.

    [Fact]
    public void CompressedFourHourDay_ExactAttendance_CountsTheFullFourHoursAsRegular()
    {
        // "Compressed shift" -- e.g. a part-time arrangement where a scheduled day is only 4h
        // (240 min) instead of the usual 8h, and that shorter span IS the employee's whole day.
        var shiftStart = new DateTime(2026, 1, 1, 8, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 1, 12, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, maxWorkingMinutes: 240);

        var result = Run(context);

        result.TotalMinutes.Should().Be(240);
    }

    [Fact]
    public void CompressedFourHourDay_AttendanceRunsLong_IsCappedToTheCompressedLimit()
    {
        // Employee stays past their compressed 4h day (e.g. 5h actually attended) -- only the
        // scheduled 240 minutes count as Regular; the extra time is not silently absorbed here
        // (it would need to go through OT separately, out of scope for this pipeline).
        var shiftStart = new DateTime(2026, 1, 1, 8, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 1, 12, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, maxWorkingMinutes: 240);
        context.CanonicalTimeRange = Range(shiftStart, shiftStart.AddHours(5)); // worked 1h past the compressed day

        var result = Run(context);

        result.TotalMinutes.Should().Be(240);
    }

    [Fact]
    public void CompressedWorkweek_TenHourDay_CountsTheFullTenHoursAsRegular()
    {
        // "Compressed workweek" -- e.g. 4x10: employees render 10h/day so they can take a 2nd
        // day off instead of the usual 1. The whole 10h (600 min) is the scheduled day, not OT.
        var shiftStart = new DateTime(2026, 1, 1, 7, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 1, 17, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, maxWorkingMinutes: 600);

        var result = Run(context);

        result.TotalMinutes.Should().Be(600);
    }

    [Fact]
    public void ExtendedHours_TwelveHourSecurityGuardShift_CountsTheFullTwelveHoursAsRegular()
    {
        // Security-guard-style duty: a single 12h shift (60 * 12 = 720 min) still counts as one
        // regular day, not automatically split into 8h regular + 4h OT.
        var shiftStart = new DateTime(2026, 1, 1, 6, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 1, 18, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, maxWorkingMinutes: 720);

        var result = Run(context);

        result.TotalMinutes.Should().Be(720);
    }

    [Fact]
    public void ExtendedHours_TwelveHourShift_ShortAttendance_ReportsOnlyWhatWasActuallyWorked()
    {
        // Attendance short of the full 12h isn't padded up to the shift's allotment.
        var shiftStart = new DateTime(2026, 1, 1, 6, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 1, 18, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, maxWorkingMinutes: 720);
        context.CanonicalTimeRange = Range(shiftStart, shiftEnd.AddMinutes(-10)); // clocked out 10 min early

        var result = Run(context);

        result.TotalMinutes.Should().Be(710);
    }

    // --- Broken (SPLIT) shift with an allowable break window ------------------------------
    // A SPLIT shift's own two segments don't change how AM/PM break windows are matched
    // (AmBreakExtractor/PmBreakExtractor don't branch on ShiftType), but a split/"broken" shift
    // is the realistic case where a mid-day allowable break window matters: TimeAllowance.
    // SnackBreakAllowance (15 min, current default) widens the configured break start/end on
    // both sides when matching an actual punch-out/in gap, so the employee doesn't need to hit
    // the break window to the exact minute for it to be captured and paid.

    [Fact]
    public void BrokenShift_AmBreakGapWithinTheAllowanceWindow_IsBridgedAndPaid()
    {
        var shiftStart = new DateTime(2026, 1, 1, 8, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 1, 17, 0, 0); // 9h span
        var context = CreateContext(shiftStart, shiftEnd, maxWorkingMinutes: 540);
        context.Payload.Data.CurrentShift.ShiftType = TimeShiftType.SPLIT;
        context.Payload.Data.CurrentShift.WithAMBreak = BreakMode.PAID_BREAK;
        context.Payload.Data.CurrentShift.AMBreakStartTime = new DateTime(2026, 1, 1, 10, 0, 0);
        context.Payload.Data.CurrentShift.AMBreakEndTime = new DateTime(2026, 1, 1, 10, 15, 0); // 15-min configured window
        // Actual punch-out/in gap is shifted 5 minutes later than configured, but still within
        // the +-15-minute SnackBreakAllowance on each side ([09:45, 10:30]).
        var gapStart = new DateTime(2026, 1, 1, 10, 5, 0);
        var gapEnd = new DateTime(2026, 1, 1, 10, 20, 0);
        context.CanonicalTimeRange = TimeRange.Set(new TimeRecordCollection
        {
            new TimeRecord(shiftStart, gapStart),
            new TimeRecord(gapEnd, shiftEnd),
        });

        var result = Run(context);

        result.TotalMinutes.Should().Be(540); // the 15-min gap is bridged back in -- full 9h paid
        context.Payload.Ledger.GetByTag("AM_BREAK", context).TotalMinutes.Should().Be(15);
    }

    [Fact]
    public void BrokenShift_BreakGapOutsideTheAllowanceWindow_IsNotBridged()
    {
        var shiftStart = new DateTime(2026, 1, 1, 8, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 1, 17, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, maxWorkingMinutes: 540);
        context.Payload.Data.CurrentShift.ShiftType = TimeShiftType.SPLIT;
        context.Payload.Data.CurrentShift.WithAMBreak = BreakMode.PAID_BREAK;
        context.Payload.Data.CurrentShift.AMBreakStartTime = new DateTime(2026, 1, 1, 10, 0, 0);
        context.Payload.Data.CurrentShift.AMBreakEndTime = new DateTime(2026, 1, 1, 10, 15, 0);
        // This gap is well outside the allowance-widened window ([09:45, 10:30]) -- e.g. an
        // early mid-morning errand, not the scheduled break -- so it's never captured as
        // AM_BREAK and the 15 minutes it costs are never bridged back in.
        var gapStart = new DateTime(2026, 1, 1, 8, 30, 0);
        var gapEnd = new DateTime(2026, 1, 1, 8, 45, 0);
        context.CanonicalTimeRange = TimeRange.Set(new TimeRecordCollection
        {
            new TimeRecord(shiftStart, gapStart),
            new TimeRecord(gapEnd, shiftEnd),
        });

        var result = Run(context);

        result.TotalMinutes.Should().Be(525); // 540 - the un-bridged 15-min gap
        context.Payload.Ledger.GetByTag("AM_BREAK", context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void BasedOnTimeInDayType_HolidayTouchingShift_RecordsTheWholeShiftAsHolidayOnTheLedger()
    {
        var employeeId = NewEmployeeId();
        var holidayDate = new DateOnly(2026, 1, 2);
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, holidayDate));
        var shiftStart = new DateTime(2026, 1, 1, 22, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 2, 6, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnTimeInDayType, holidays, employeeId);
        context.Payload.Data.CurrentShift.ShiftDate = holidayDate;
        context.Payload.Data.CurrentDate = holidayDate;

        var result = Run(context);

        // WorkTimePipeline itself always returns the full, unsplit regular time — the routing
        // to Regular vs. Holiday columns happens one layer up (see DtrRowIntegrationTests).
        result.TotalMinutes.Should().Be(480);
        context.Payload.Ledger.GetByTag("holiday_portion_LEGAL", context).TotalMinutes.Should().Be(480);
        context.Payload.Ledger.GetByTag("non_holiday_portion", context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void BasedOnActualWorkHours_BoundaryCrossingShift_RecordsTheRealSplitOnTheLedger()
    {
        var employeeId = NewEmployeeId();
        var holidayDate = new DateOnly(2026, 1, 2);
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, holidayDate));
        var shiftStart = new DateTime(2026, 1, 1, 22, 0, 0);
        var shiftEnd = new DateTime(2026, 1, 2, 6, 0, 0);
        var context = CreateContext(shiftStart, shiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);
        context.Payload.Data.CurrentShift.ShiftDate = holidayDate;
        context.Payload.Data.CurrentDate = holidayDate;

        var result = Run(context);

        result.TotalMinutes.Should().Be(480); // still the full, unsplit range
        context.Payload.Ledger.GetByTag("holiday_portion_LEGAL", context).TotalMinutes.Should().Be(360); // Jan2 00:00-06:00
        context.Payload.Ledger.GetByTag("non_holiday_portion", context).TotalMinutes.Should().Be(120); // Jan1 22:00-00:00
    }
}
