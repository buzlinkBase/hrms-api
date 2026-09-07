using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Policies.OT;

/// <summary>
/// TrimOTForFirst8HrPolicy — when the employee hasn't yet claimed a full regular shift
/// (RegularHourPolicy's ledger value &lt; shift.MaxWorkingMinutes), borrows the shortfall off
/// the front of the OT range as "RegularTimeTopUp" so the regular-hours quota gets filled
/// before anything counts as OT; the remainder is retagged "OT_after_RegularFulfilled".
/// </summary>
public class TrimOTForFirst8HrPolicyTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 1, 17, 0, 0); // 480-min shift (MaxWorkingMinutes)

    private class AlwaysTrueSpec : IRuleSpecification
    {
        public bool IsSatisfiedBy(TimeRange input, TimeContext context) => true;
    }

    private class AlwaysFalseSpec : IRuleSpecification
    {
        public bool IsSatisfiedBy(TimeRange input, TimeContext context) => false;
    }

    [Fact]
    public void RegularAlreadyFulfilled_ReturnsOTRangeUnchanged()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        var regTime = Range(ShiftStart, ShiftStart.AddMinutes(480)); // fully claimed
        context.Payload.Ledger.Record(TimeRangeLedger.CreateKey<RegularHourPolicy>(context), regTime);
        var otStart = new DateTime(2026, 1, 1, 17, 0, 0);
        var otRange = Range(otStart, otStart.AddMinutes(120));

        var result = new TrimOTForFirst8HrPolicy(new AlwaysTrueSpec()).Apply(otRange, context);

        result.Should().Be(otRange);
    }

    [Fact]
    public void RegularUnderFulfilled_BorrowsTheShortfallFromTheStartOfOT()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        var regTime = Range(ShiftStart, ShiftStart.AddMinutes(400)); // 80-min shortfall against 480
        context.Payload.Ledger.Record(TimeRangeLedger.CreateKey<RegularHourPolicy>(context), regTime);
        var otStart = new DateTime(2026, 1, 1, 17, 0, 0);
        var otRange = Range(otStart, otStart.AddMinutes(180)); // 180-min OT block

        var result = new TrimOTForFirst8HrPolicy(new AlwaysTrueSpec()).Apply(otRange, context);

        result.TotalMinutes.Should().Be(100); // 180 - 80 shortfall
        var topup = context.Payload.Ledger.GetByTag("RegularTimeTopUp", context);
        topup.TotalMinutes.Should().Be(80);
        var finalOT = context.Payload.Ledger.GetByTag("FinalOT", context);
        finalOT.TotalMinutes.Should().Be(100);
    }

    [Fact]
    public void NoRegularTimeClaimedAtAll_BorrowsFullShiftShortfallOrWhateverOTCovers()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480); // no RegularHourPolicy ledger entry seeded
        var otStart = new DateTime(2026, 1, 1, 17, 0, 0);
        var otRange = Range(otStart, otStart.AddMinutes(60)); // OT smaller than the full 480 shortfall

        var result = new TrimOTForFirst8HrPolicy(new AlwaysTrueSpec()).Apply(otRange, context);

        result.IsEmpty().Should().BeTrue(); // entirely consumed topping up regular time
        context.Payload.Ledger.GetByTag("RegularTimeTopUp", context).TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void CachedLedgerValue_ReturnsItDirectlyWithoutRecomputing()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        var sentinel = new TimeRange(999);
        context.Payload.Ledger.Record(TimeRangeLedger.CreateKey<TrimOTForFirst8HrPolicy>(context), sentinel);
        // Deliberately no RegularHourPolicy entry — if the cache weren't honored this would NRE-free
        // but produce a completely different (non-999) result.
        var otRange = Range(new DateTime(2026, 1, 1, 17, 0, 0), new DateTime(2026, 1, 1, 18, 0, 0));

        var result = new TrimOTForFirst8HrPolicy(new AlwaysTrueSpec()).Apply(otRange, context);

        result.TotalMinutes.Should().Be(999);
    }

    [Fact]
    public void SpecNotSatisfied_ReturnInputBehavior_ReturnsOTUnchangedWithNoLedgerWrites()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        var otRange = Range(new DateTime(2026, 1, 1, 17, 0, 0), new DateTime(2026, 1, 1, 18, 0, 0));

        var result = new TrimOTForFirst8HrPolicy(new AlwaysFalseSpec(), SpecFailureBehavior.ReturnInput).Apply(otRange, context);

        result.Should().Be(otRange);
        context.Payload.Ledger.GetByTag("RegularTimeTopUp", context).IsEmpty().Should().BeTrue();
    }
}
