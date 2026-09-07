using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Policies.OT;

/// <summary>
/// AutoComputeOvertimePolicy — gated by a passed-in spec (IsSystemAutoComputeOT in production),
/// then delegates to the Post -&gt; Pre -&gt; Outside Chain of Responsibility, applies
/// shift.OverTimeThreshold (default 60 min) as a pass/fail gate on the chain's total, and caches
/// the result under its own type-keyed ledger entry. CompanyPolicy.OTInclusionPolicy defaults to
/// UseEarlyClockIn (enum's first value), so with no explicit policy PostShiftOTHandler's
/// CanHandle fails and the chain falls through to PreShiftOTHandler.
/// </summary>
public class AutoComputeOvertimePolicyTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 1, 16, 0, 0);
    private static readonly DateTime PreStart = new(2026, 1, 1, 6, 0, 0);

    private class AlwaysFalseSpec : IRuleSpecification
    {
        public bool IsSatisfiedBy(TimeRange input, TimeContext context) => false;
    }

    private static TimeRange MultiRecordRange(params (DateTime Start, DateTime End)[] spans)
    {
        var trc = new TimeRecordCollection();
        foreach (var (start, end) in spans) trc.Add(new TimeRecord(start, end));
        return TimeRange.Set(trc);
    }

    [Fact]
    public void PreShiftWorkMeetsThreshold_ReturnsTheComputedOT()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        // 2h pre-shift (>= 60-min default OverTimeThreshold), separated from the shift record.
        context.CanonicalTimeRange = MultiRecordRange((PreStart, PreStart.AddHours(2)));

        var result = new AutoComputeOvertimePolicy(new IsSystemAutoComputeOT()).Apply(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(120);
        context.Payload.Ledger.GetByKey(TimeRangeLedger.CreateKey<AutoComputeOvertimePolicy>(context)).Value!.TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void PreShiftWorkBelowThreshold_ReturnsEmpty()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        // 30 min pre-shift < 60-min default OverTimeThreshold.
        context.CanonicalTimeRange = MultiRecordRange((ShiftStart.AddMinutes(-30), ShiftStart));

        var result = new AutoComputeOvertimePolicy(new IsSystemAutoComputeOT()).Apply(TimeRange.Empty, context);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void SecondApplyCall_ReturnsCachedResultWithoutRecomputing()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.CanonicalTimeRange = MultiRecordRange((PreStart, PreStart.AddHours(2)));
        var policy = new AutoComputeOvertimePolicy(new IsSystemAutoComputeOT());
        var first = policy.Apply(TimeRange.Empty, context);

        // Mutate the underlying data after the first call — a genuinely fresh computation
        // would now see no pre-shift work at all.
        context.CanonicalTimeRange = MultiRecordRange((ShiftStart, ShiftEnd));
        var second = policy.Apply(TimeRange.Empty, context);

        second.TotalMinutes.Should().Be(first.TotalMinutes).And.Be(120);
    }

    [Fact]
    public void SpecNotSatisfied_ReturnsEmpty()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.CanonicalTimeRange = MultiRecordRange((PreStart, PreStart.AddHours(2)));

        var result = new AutoComputeOvertimePolicy(new AlwaysFalseSpec()).Apply(TimeRange.Empty, context);

        result.IsEmpty().Should().BeTrue();
    }
}
