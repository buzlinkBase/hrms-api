using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Policies.OT;

/// <summary>
/// PostShiftOTHandler — CanHandle/Process are protected (OTComputationHandlerBase's Chain of
/// Responsibility pattern), so these tests drive the handler through its public Handle(input,
/// context) entry point, the same way AutoComputeOvertimePolicy's real handler chain does.
/// CanHandle gates on CompanyPolicy/ClientPolicy.OvertimeInclusionPolicy == UsePostShiftWork;
/// with no next handler wired in these tests, a CanHandle miss surfaces as an Empty result —
/// distinguished from a CanHandle-true-but-nothing-to-capture Empty by varying whether
/// post-shift work data is present. Process scans "usable" canonical-range records (each
/// representing a discrete work session — the `.Where(r => r.StartTime >= ...)` filter matches
/// a record only by its OWN start time, so it never partially crops a single merged block) for
/// anything starting at/after the OT-start time plus TimeAllowance.OTTimeCaptureAllowanceMinutes
/// (-30, so 30 minutes lenient). OTRequireTimeIn is left false in these tests -&gt; FixedOT
/// provider, so OT start is simply shift.StartTime + shift.MaxWorkingMinutes (== shift.EndTime
/// here).
/// </summary>
public class PostShiftOTHandlerTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 1, 16, 0, 0); // 480-min shift
    private static readonly DateTime PostStart = new(2026, 1, 1, 16, 0, 0);

    private static TimeRange MultiRecordRange(params (DateTime Start, DateTime End)[] spans)
    {
        var trc = new TimeRecordCollection();
        foreach (var (start, end) in spans) trc.Add(new TimeRecord(start, end));
        return TimeRange.Set(trc);
    }

    private static TimeContext BuildContext(OvertimeInclusionPolicy? policy = null, bool withPostShiftWork = true)
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.OTRequireTimeIn = false;
        if (policy.HasValue) context.Payload.Data.CompanyPolicy.OTInclusionPolicy = policy.Value;
        // A record touching/adjacent to the shift-end record would get merged into one block by
        // MergeOverlapping (defeating the per-record StartTime filter), so the post-shift record
        // is kept separate from (not touching) the scheduled 8:00-16:00 block.
        context.CanonicalTimeRange = withPostShiftWork
            ? MultiRecordRange((ShiftStart, ShiftStart.AddHours(4)), (PostStart, PostStart.AddHours(2)))
            : MultiRecordRange((ShiftStart, ShiftStart.AddHours(4)));
        return context;
    }

    [Fact]
    public void Handle_PolicyUsePostShiftWorkWithPostShiftWork_CapturesThePostShiftPortion()
    {
        var context = BuildContext(OvertimeInclusionPolicy.UsePostShiftWork);

        var result = new PostShiftOTHandler().Handle(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(120);
        context.Payload.Ledger.GetByTag(nameof(PostShiftOTHandler), context).TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void Handle_PolicyNotUsePostShiftWork_ReturnsEmptyEvenWithPostShiftWorkPresent()
    {
        var context = BuildContext(OvertimeInclusionPolicy.UseEarlyClockIn);

        new PostShiftOTHandler().Handle(TimeRange.Empty, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void Handle_ClientPolicyOverridesCompanyPolicy()
    {
        var context = BuildContext(OvertimeInclusionPolicy.UseEarlyClockIn); // company says no
        var clientId = Guid.NewGuid();
        context.Payload.Data.Employee.ClientId = clientId;
        context.Payload.Provider.ClientPolicyProvider = new ClientPolicyProvider(new Dictionary<ClientPolicyKey, ClientPolicyRule>
        {
            [new ClientPolicyKey(clientId)] = new ClientPolicyRule { OvertimeInclusionPolicy = OvertimeInclusionPolicy.UsePostShiftWork },
        });

        var result = new PostShiftOTHandler().Handle(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void Handle_PolicyMatchesButNoWorkPastShiftEnd_ReturnsEmpty()
    {
        var context = BuildContext(OvertimeInclusionPolicy.UsePostShiftWork, withPostShiftWork: false);

        new PostShiftOTHandler().Handle(TimeRange.Empty, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void Handle_OTRequireTimeInWithNoConfiguredOTStartTime_ReturnsEmpty()
    {
        var context = BuildContext(OvertimeInclusionPolicy.UsePostShiftWork);
        context.Payload.Data.CurrentShift.OTRequireTimeIn = true; // AutoComputeOTTimeInProvider path
        context.Payload.Data.CurrentShift.OTStartTime = null;

        new PostShiftOTHandler().Handle(TimeRange.Empty, context).IsEmpty().Should().BeTrue();
    }
}
