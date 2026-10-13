using Hrms.adms.Services.Subscriptions;
using NSubstitute;
using Onepunch.Common.Lib.Subscriptions;

namespace hrms.test.SubscriptionTests;

/// <summary>
/// A lapsed company's devices keep their punches stored for a while -- then they're refused, and
/// the device keeps them until the company is paid up.
/// </summary>
public class PunchAcceptanceTests
{
    private static readonly DateTime Lock = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    private static Task<bool> AcceptsOnDayAsync(int day, EntitlementSnapshot snapshot)
    {
        var entitlements = Substitute.For<IEntitlementProvider>();
        entitlements.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(snapshot);
        var service = new PunchAcceptanceService(entitlements, new FixedClock(Lock.AddDays(day)));
        return service.AcceptsPunchesAsync(Guid.NewGuid(), $"SN-{Guid.NewGuid():N}", CancellationToken.None);
    }

    private static EntitlementSnapshot Lapsed() => new()
    {
        Status = "Expired",
        AccessMode = SubscriptionAccessMode.ReadOnly,
        ReadOnlySinceUtc = Lock,
        PunchesAcceptedUntilUtc = Lock.AddDays(60),
    };

    [Fact]
    public async Task StoredWithinTheWindow() =>
        (await AcceptsOnDayAsync(59, Lapsed())).Should().BeTrue();

    [Fact]
    public async Task RefusedAfterTheWindow() =>
        (await AcceptsOnDayAsync(61, Lapsed())).Should().BeFalse();

    private static Task<bool> HasHrmsAsync(bool hrms)
    {
        var snapshot = new EntitlementSnapshot
        {
            Features = new() { [EntitlementKeys.HrmsCore] = hrms },
        };
        var entitlements = Substitute.For<IEntitlementProvider>();
        entitlements.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(snapshot);
        var service = new PunchAcceptanceService(entitlements, new FixedClock(Lock));
        return service.HasHrmsAsync(Guid.NewGuid(), CancellationToken.None);
    }

    [Fact]
    public async Task HrmsOn_KeepsWhatTheDevicePushes() =>
        (await HasHrmsAsync(true)).Should().BeTrue();

    [Fact]
    public async Task HrmsOff_DropsWhatTheDevicePushes() =>
        (await HasHrmsAsync(false)).Should().BeFalse();

    [Fact]
    public async Task AlwaysStoredForAnActiveCompany() =>
        (await AcceptsOnDayAsync(400, new EntitlementSnapshot { Status = "Active", AccessMode = SubscriptionAccessMode.Full }))
            .Should().BeTrue();
}
