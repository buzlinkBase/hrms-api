using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Policies.OT;

/// <summary>
/// PreShiftOTHandler — CanHandle gates on ShiftType != SPLIT AND
/// CompanyPolicy/ClientPolicy.OvertimeInclusionPolicy == UseEarlyClockIn (the enum's default
/// value, so a fresh CompanyPolicyRule already satisfies it). Process crops any usable record
/// that starts before AND ends at-or-before the shift's scheduled start into a
/// "[Pre-Shift OT]"-tagged slice. Driven through the public Handle(input, context) entry point
/// for the same reason as PostShiftOTHandlerTests (CanHandle/Process are protected).
/// </summary>
public class PreShiftOTHandlerTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 1, 16, 0, 0);
    private static readonly DateTime PreStart = new(2026, 1, 1, 7, 0, 0);

    private static TimeRange MultiRecordRange(params (DateTime Start, DateTime End)[] spans)
    {
        var trc = new TimeRecordCollection();
        foreach (var (start, end) in spans) trc.Add(new TimeRecord(start, end));
        return TimeRange.Set(trc);
    }

    private static TimeContext BuildContext(bool withPreShiftWork = true, TimeShiftType shiftType = TimeShiftType.FIXED)
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 480);
        context.Payload.Data.CurrentShift.ShiftType = shiftType;
        context.CanonicalTimeRange = withPreShiftWork
            ? MultiRecordRange((PreStart, ShiftStart), (ShiftStart.AddHours(1), ShiftStart.AddHours(5)))
            : MultiRecordRange((ShiftStart, ShiftStart.AddHours(4)));
        return context;
    }

    [Fact]
    public void Handle_DefaultPolicyIsUseEarlyClockIn_CapturesThePreShiftPortion()
    {
        var context = BuildContext();

        var result = new PreShiftOTHandler().Handle(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(60); // 07:00-08:00
        result.TimeRecords.Single().Tag.Should().Contain("[Pre-Shift OT]");
    }

    [Fact]
    public void Handle_PolicyNotUseEarlyClockIn_ReturnsEmptyEvenWithPreShiftWorkPresent()
    {
        var context = BuildContext();
        context.Payload.Data.CompanyPolicy.OTInclusionPolicy = OvertimeInclusionPolicy.UsePostShiftWork;

        new PreShiftOTHandler().Handle(TimeRange.Empty, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void Handle_SplitShift_NeverHandles()
    {
        var context = BuildContext(shiftType: TimeShiftType.SPLIT);

        new PreShiftOTHandler().Handle(TimeRange.Empty, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void Handle_ClientPolicyOverridesCompanyPolicy()
    {
        var context = BuildContext();
        context.Payload.Data.CompanyPolicy.OTInclusionPolicy = OvertimeInclusionPolicy.UsePostShiftWork; // company says no
        var clientId = Guid.NewGuid();
        context.Payload.Data.Employee.ClientId = clientId;
        context.Payload.Provider.ClientPolicyProvider = new ClientPolicyProvider(new Dictionary<ClientPolicyKey, ClientPolicyRule>
        {
            [new ClientPolicyKey(clientId)] = new ClientPolicyRule { OvertimeInclusionPolicy = OvertimeInclusionPolicy.UseEarlyClockIn },
        });

        var result = new PreShiftOTHandler().Handle(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void Handle_NoWorkBeforeShiftStart_ReturnsEmpty()
    {
        var context = BuildContext(withPreShiftWork: false);

        new PreShiftOTHandler().Handle(TimeRange.Empty, context).IsEmpty().Should().BeTrue();
    }
}
