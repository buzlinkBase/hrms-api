using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Policies.Breaks;

/// <summary>
/// AmBreakExtractor/PmBreakExtractor/LunchBreakExtractor — the PAID_BREAK detection path
/// (ExcludeBreakTime, already covered by WorkTimePipelineTests, handles UNPAID_BREAK
/// separately and never touches these). Each scans the gaps BETWEEN punches (not a single
/// continuous attendance range) for one falling inside an allowance-padded break window.
/// TimeAllowance.SnackBreakAllowance (15 min, AM/PM) and
/// TimeAllowance.LunchPaidBreakCaptureAllowance (0 min) are read as their current static
/// defaults rather than mutated — they're process-wide mutable state
/// (hrms.Domain\TimeAllowance.cs), so changing them here would leak into other tests.
/// </summary>
public class BreakExtractorsTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 1, 17, 0, 0);

    private static TimeRecordCollection Punches(DateTime firstEnd, DateTime secondStart) => new()
    {
        new TimeRecord(ShiftStart, firstEnd),
        new TimeRecord(secondStart, ShiftEnd),
    };

    [Fact]
    public void AmBreakExtractor_PaidBreakConfigured_CapturesThePunchGap()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        var shift = context.Payload.Data.CurrentShift;
        shift.WithAMBreak = BreakMode.PAID_BREAK;
        shift.AMBreakStartTime = new DateTime(2026, 1, 1, 10, 0, 0);
        shift.AMBreakEndTime = new DateTime(2026, 1, 1, 10, 15, 0);
        var punches = Punches(shift.AMBreakStartTime.Value, shift.AMBreakEndTime.Value);

        var result = new AmBreakExtractor(context).Extract(punches, shift);

        result.Should().ContainSingle();
        result[0].TotalMinutes.Should().Be(15);
        context.Payload.Ledger.GetByTag("AM_BREAK", context).TotalMinutes.Should().Be(15);
    }

    [Fact]
    public void AmBreakExtractor_NotConfiguredAsPaid_ReturnsEmpty()
    {
        var context = CreateContext(ShiftStart, ShiftEnd); // WithAMBreak defaults to BreakMode.NONE
        var shift = context.Payload.Data.CurrentShift;
        shift.AMBreakStartTime = new DateTime(2026, 1, 1, 10, 0, 0);
        shift.AMBreakEndTime = new DateTime(2026, 1, 1, 10, 15, 0);
        var punches = Punches(shift.AMBreakStartTime.Value, shift.AMBreakEndTime.Value);

        var result = new AmBreakExtractor(context).Extract(punches, shift);

        result.Should().BeEmpty();
    }

    [Fact]
    public void PmBreakExtractor_PaidBreakConfigured_CapturesThePunchGap()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        var shift = context.Payload.Data.CurrentShift;
        shift.WithPMBreakTime = BreakMode.PAID_BREAK;
        shift.PMBreakStartTime = new DateTime(2026, 1, 1, 15, 0, 0);
        shift.PMBreakEndTime = new DateTime(2026, 1, 1, 15, 15, 0);
        var punches = Punches(shift.PMBreakStartTime.Value, shift.PMBreakEndTime.Value);

        var result = new PmBreakExtractor(context).Extract(punches, shift);

        result.Should().ContainSingle();
        result[0].TotalMinutes.Should().Be(15);
        context.Payload.Ledger.GetByTag("PM_BREAK", context).TotalMinutes.Should().Be(15);
    }

    [Fact]
    public void LunchBreakExtractor_PaidBreakConfigured_CapturesThePunchGapExactly()
    {
        // LunchPaidBreakCaptureAllowance defaults to 0 -> no padding, unlike AM/PM's 15 min.
        var context = CreateContext(ShiftStart, ShiftEnd);
        var shift = context.Payload.Data.CurrentShift;
        shift.LunchBreakOption = BreakMode.PAID_BREAK;
        shift.LunchStartTime = new DateTime(2026, 1, 1, 12, 0, 0);
        shift.LunchEndTime = new DateTime(2026, 1, 1, 13, 0, 0);
        var punches = Punches(shift.LunchStartTime.Value, shift.LunchEndTime.Value);

        var result = new LunchBreakExtractor(context).Extract(punches, shift);

        result.Should().ContainSingle();
        result[0].TotalMinutes.Should().Be(60);
        context.Payload.Ledger.GetByTag("LUNCH_BREAK", context).TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void LunchBreakExtractor_NotConfiguredAsPaid_ReturnsEmpty()
    {
        var context = CreateContext(ShiftStart, ShiftEnd); // LunchBreakOption defaults to BreakMode.NONE
        var shift = context.Payload.Data.CurrentShift;
        shift.LunchStartTime = new DateTime(2026, 1, 1, 12, 0, 0);
        shift.LunchEndTime = new DateTime(2026, 1, 1, 13, 0, 0);
        var punches = Punches(shift.LunchStartTime.Value, shift.LunchEndTime.Value);

        var result = new LunchBreakExtractor(context).Extract(punches, shift);

        result.Should().BeEmpty();
    }
}
