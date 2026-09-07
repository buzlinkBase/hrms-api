using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Policies.OT;

/// <summary>
/// OutsideShiftOverTimeHandler — CanHandle gates on OvertimeInclusionPolicy ==
/// UseAllExcessOver8Hours; Process combines PreShiftOTUnrestrictedHandler +
/// PostShiftOTUnrestrictedHandler, which call Process directly (bypassing their own CanHandle
/// gates entirely) so pre/post capture always both run once this handler itself is selected.
/// </summary>
public class OutsideShiftOverTimeHandlerTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 1, 16, 0, 0);
    private static readonly DateTime PreStart = new(2026, 1, 1, 7, 0, 0);
    private static readonly DateTime PostStart = new(2026, 1, 1, 16, 0, 0);

    private static TimeRange MultiRecordRange(params (DateTime Start, DateTime End)[] spans)
    {
        var trc = new TimeRecordCollection();
        foreach (var (start, end) in spans) trc.Add(new TimeRecord(start, end));
        return TimeRange.Set(trc);
    }

    private static TimeContext BuildContext(OvertimeInclusionPolicy? policy)
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.OTRequireTimeIn = false; // FixedOT for the post-shift half
        if (policy.HasValue) context.Payload.Data.CompanyPolicy.OTInclusionPolicy = policy.Value;
        // Gaps on both sides of each record avoid MergeOverlapping fusing them into one block
        // (which would defeat both the pre-shift crop condition and the post-shift start filter).
        context.CanonicalTimeRange = MultiRecordRange(
            (PreStart, PreStart.AddMinutes(50)),
            (PostStart, PostStart.AddHours(2)));
        return context;
    }

    [Fact]
    public void Handle_PolicyUseAllExcessOver8Hours_CombinesPreAndPostShiftOT()
    {
        var context = BuildContext(OvertimeInclusionPolicy.UseAllExcessOver8Hours);

        var result = new OutsideShiftOverTimeHandler().Handle(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(180); // 60 pre + 120 post
    }

    [Fact]
    public void Handle_PolicyNotUseAllExcessOver8Hours_ReturnsEmpty()
    {
        var context = BuildContext(OvertimeInclusionPolicy.UseEarlyClockIn);

        new OutsideShiftOverTimeHandler().Handle(TimeRange.Empty, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void Handle_ClientPolicyOverridesCompanyPolicy()
    {
        var context = BuildContext(OvertimeInclusionPolicy.UseEarlyClockIn); // company says no
        var clientId = Guid.NewGuid();
        context.Payload.Data.Employee.ClientId = clientId;
        context.Payload.Provider.ClientPolicyProvider = new ClientPolicyProvider(new Dictionary<ClientPolicyKey, ClientPolicyRule>
        {
            [new ClientPolicyKey(clientId)] = new ClientPolicyRule { OvertimeInclusionPolicy = OvertimeInclusionPolicy.UseAllExcessOver8Hours },
        });

        var result = new OutsideShiftOverTimeHandler().Handle(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(180);
    }

    [Fact]
    public void Handle_NoWorkOutsideScheduledShift_ReturnsEmpty()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.OTRequireTimeIn = false;
        context.Payload.Data.CompanyPolicy.OTInclusionPolicy = OvertimeInclusionPolicy.UseAllExcessOver8Hours;
        context.CanonicalTimeRange = MultiRecordRange((ShiftStart, ShiftEnd));

        new OutsideShiftOverTimeHandler().Handle(TimeRange.Empty, context).IsEmpty().Should().BeTrue();
    }
}
