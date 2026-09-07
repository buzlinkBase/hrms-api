using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Policies.Late;

/// <summary>
/// WholeDayLateHandler/HalfDayLateHandler — the actual late-deduction truncation math
/// LatePolicy delegates to once a late slice is detected (see WorkTimePipelineTests for
/// confirmation that, with both CompanyPolicy flags at their default off, a late clock-in is
/// recorded but not deducted — these tests cover the opt-in behavior once a company turns
/// either flag on).
/// </summary>
public class LateDeductionHandlersTests : DtrTestBase
{
    private static TimeContext BuildContext(
        bool wholeDayOn = false, double wholeDayThreshold = 0,
        bool halfDayOn = false, double halfDayThreshold = 0,
        double maxWorkingMinutes = 480)
    {
        var context = CreateContext(
            new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 16, 0, 0),
            maxWorkingMinutes: maxWorkingMinutes);
        context.Payload.Data.CompanyPolicy.IsWholeDayLateOn = wholeDayOn;
        context.Payload.Data.CompanyPolicy.WholeDayLateThresholdMinutes = wholeDayThreshold;
        context.Payload.Data.CompanyPolicy.IsHalfDayLateOn = halfDayOn;
        context.Payload.Data.CompanyPolicy.HalfDayLateThresholdMinutes = halfDayThreshold;
        return context;
    }

    // --- WholeDayLateHandler ---------------------------------------------------------------

    [Fact]
    public void WholeDayLateHandler_FlagOff_CannotHandleRegardlessOfLateSlice()
    {
        var context = BuildContext(wholeDayOn: false, wholeDayThreshold: 240);
        var lateSlice = new TimeRange(300);

        new WholeDayLateHandler().CanHandle(lateSlice, context).Should().BeFalse();
    }

    [Fact]
    public void WholeDayLateHandler_LateSliceMeetsThreshold_ZeroesRegularTime()
    {
        var context = BuildContext(wholeDayOn: true, wholeDayThreshold: 240, maxWorkingMinutes: 480);
        var lateSlice = new TimeRange(300); // >= 240-minute threshold
        var regTime = Range(new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 16, 0, 0));

        var handler = new WholeDayLateHandler();
        handler.CanHandle(lateSlice, context).Should().BeTrue();

        var result = handler.Apply(regTime, lateSlice, context);

        result.TotalMinutes.Should().Be(0);
        context.Payload.Ledger.GetByTag("late", context).TotalMinutes.Should().Be(480); // the full shift is forfeited
    }

    [Fact]
    public void WholeDayLateHandler_LateSliceBelowThreshold_CannotHandle()
    {
        var context = BuildContext(wholeDayOn: true, wholeDayThreshold: 240);
        var lateSlice = new TimeRange(120); // below the 240-minute threshold

        new WholeDayLateHandler().CanHandle(lateSlice, context).Should().BeFalse();
    }

    // --- HalfDayLateHandler ------------------------------------------------------------------

    [Fact]
    public void HalfDayLateHandler_FlagOff_CannotHandle()
    {
        var context = BuildContext(halfDayOn: false, halfDayThreshold: 60);
        var lateSlice = new TimeRange(90);

        new HalfDayLateHandler().CanHandle(lateSlice, context).Should().BeFalse();
    }

    [Fact]
    public void HalfDayLateHandler_LateSliceWithinRange_CropsRegularTimeToHalfTheShift()
    {
        var context = BuildContext(halfDayOn: true, halfDayThreshold: 60, maxWorkingMinutes: 480);
        var lateSlice = new TimeRange(90); // >= 60-minute threshold, <= half the 480-minute shift (240)
        var regStart = new DateTime(2026, 1, 1, 9, 30, 0);
        var regTime = Range(regStart, regStart.AddMinutes(390)); // what's left after a 90-min late arrival

        var handler = new HalfDayLateHandler();
        handler.CanHandle(lateSlice, context).Should().BeTrue();

        var result = handler.Apply(regTime, lateSlice, context);

        result.TotalMinutes.Should().Be(240); // MaxWorkingMinutes / 2
        result.TimeRecords.Single().StartTime.Should().Be(regStart);
        context.Payload.Ledger.GetByTag("late", context).TotalMinutes.Should().Be(240); // 480 - 240 cropped
    }

    [Fact]
    public void HalfDayLateHandler_LateSliceExceedsHalfTheShift_CannotHandle()
    {
        // WholeDayLateHandler is meant to own this case instead — see LatePolicy's handler order.
        var context = BuildContext(halfDayOn: true, halfDayThreshold: 60, maxWorkingMinutes: 480);
        var lateSlice = new TimeRange(300); // exceeds half the shift (240)

        new HalfDayLateHandler().CanHandle(lateSlice, context).Should().BeFalse();
    }

    // --- LateDeductionProcessor precedence ---------------------------------------------------

    [Fact]
    public void LateDeductionProcessor_BothFlagsOn_WholeDayTakesPrecedenceOverHalfDay()
    {
        // A late slice that satisfies BOTH handlers' thresholds — WholeDayLateHandler is tried
        // first (see LatePolicy's handler order) and should win.
        var context = BuildContext(
            wholeDayOn: true, wholeDayThreshold: 200,
            halfDayOn: true, halfDayThreshold: 60,
            maxWorkingMinutes: 480);
        var lateSlice = new TimeRange(220); // >= 200 (whole-day) and <= 240 (half-day ceiling)
        var regTime = Range(new DateTime(2026, 1, 1, 11, 40, 0), new DateTime(2026, 1, 1, 16, 0, 0));

        var processor = new LateDeductionProcessor(new ILateDeductionHandler[]
        {
            new WholeDayLateHandler(),
            new HalfDayLateHandler(),
        });

        var result = processor.Process(regTime, lateSlice, context);

        result.TotalMinutes.Should().Be(0); // WholeDayLateHandler's outcome, not HalfDay's crop
    }

    [Fact]
    public void LateDeductionProcessor_NoHandlerMatches_ReturnsRegularTimeUnchanged()
    {
        var context = BuildContext(); // both flags off
        var lateSlice = new TimeRange(30);
        var regTime = Range(new DateTime(2026, 1, 1, 8, 30, 0), new DateTime(2026, 1, 1, 16, 0, 0));

        var processor = new LateDeductionProcessor(new ILateDeductionHandler[]
        {
            new WholeDayLateHandler(),
            new HalfDayLateHandler(),
        });

        var result = processor.Process(regTime, lateSlice, context);

        result.Should().Be(regTime);
        context.Payload.Ledger.GetByTag("late", context).Should().Be(lateSlice);
    }
}
