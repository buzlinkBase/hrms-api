using hrms.test.TestSupport;
using Hrms.Core.Services;
using Hrms.Core.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Onepunch.Common.Lib.Subscriptions;

namespace hrms.test.SubscriptionTests;

/// <summary>Attendance from the mobile app is only accepted while the plan includes timekeeping.mobile.</summary>
public class AttendanceSourceGuardTests
{
    private static EntitlementSnapshot SnapshotWith(bool mobile) => new()
    {
        TenantId = Snapshots.Company,
        Status = "Active",
        AccessMode = SubscriptionAccessMode.Full,
        Features = EntitlementKeys.Features.ToDictionary(k => k, k => k != EntitlementKeys.TimekeepingMobile || mobile),
    };

    private static AttendanceSourceGuard GuardFor(bool mobile, IEntitlementProvider? provider = null) =>
        new(provider ?? Snapshots.Provider(SnapshotWith(mobile)), Snapshots.Tenant());

    // ── The guard ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LOGSOURCE.MOBILE)]
    [InlineData(LOGSOURCE.GEOFENCE)]
    public async Task MobileSources_AreRefused_WhenThePlanLacksMobileAttendance(LOGSOURCE source)
    {
        var act = () => GuardFor(mobile: false).EnsureAllowedAsync([source], CancellationToken.None);

        var refused = (await act.Should().ThrowAsync<PlanRestrictionException>()).Which;
        refused.ProblemType.Should().Be(SubscriptionProblemTypes.PlanRequired);
        refused.Extensions["feature"].Should().Be(EntitlementKeys.TimekeepingMobile);
    }

    [Theory]
    [InlineData(LOGSOURCE.MOBILE)]
    [InlineData(LOGSOURCE.GEOFENCE)]
    public async Task MobileSources_AreAccepted_WhenThePlanIncludesMobileAttendance(LOGSOURCE source)
    {
        var act = () => GuardFor(mobile: true).EnsureAllowedAsync([source], CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(LOGSOURCE.MANUAL)]
    [InlineData(LOGSOURCE.UPLOADED)]
    [InlineData(LOGSOURCE.BIOMETRIC)]
    [InlineData(LOGSOURCE.SYNC)]
    [InlineData(LOGSOURCE.OTHER)]
    public async Task OtherSources_AreNeverGated_AndCostNoEntitlementLookup(LOGSOURCE source)
    {
        var provider = Substitute.For<IEntitlementProvider>();

        await GuardFor(mobile: false, provider).EnsureAllowedAsync([source], CancellationToken.None);

        await provider.DidNotReceiveWithAnyArgs().GetAsync(default, default);
    }

    [Fact]
    public async Task ABatchWithAMobileLog_IsRefused_EvenAmongAllowedOnes()
    {
        var act = () => GuardFor(mobile: false).EnsureAllowedAsync([LOGSOURCE.MANUAL, LOGSOURCE.MOBILE], CancellationToken.None);

        await act.Should().ThrowAsync<PlanRestrictionException>();
    }

    // ── Through AttendanceService: nothing is stored when refused ────────────────────────────

    private static Attendance Log(LOGSOURCE source, int minute) => new()
    {
        Id = Guid.NewGuid(),
        BioId = 7,
        LogSource = source,
        WorkDateTime = new DateTime(2026, 3, 2, 8, minute, 0),
    };

    private static async Task<int> StoredAsync(SqliteHrmsContext db)
    {
        await using var verify = db.NewContext();
        return await verify.Attendances.CountAsync();
    }

    [Fact]
    public async Task AddRange_StoresNothing_WhenTheBatchHoldsAMobileLogTheyCannotHave()
    {
        using var db = new SqliteHrmsContext();
        await using (var context = db.NewContext())
        {
            var service = new AttendanceService(new UnitOfWorkService(context), GuardFor(mobile: false));

            var act = () => service.AddRangeAsync([Log(LOGSOURCE.MANUAL, 0), Log(LOGSOURCE.MOBILE, 1)]);

            await act.Should().ThrowAsync<PlanRestrictionException>();
            await service.CommitChangesAsync(CancellationToken.None);
        }

        (await StoredAsync(db)).Should().Be(0);
    }

    [Fact]
    public async Task AddRange_StoresMobileLogs_WhenThePlanIncludesThem()
    {
        using var db = new SqliteHrmsContext();
        await using (var context = db.NewContext())
        {
            var service = new AttendanceService(new UnitOfWorkService(context), GuardFor(mobile: true));
            await service.AddRangeAsync([Log(LOGSOURCE.MOBILE, 0)]);
            await service.CommitChangesAsync(CancellationToken.None);
        }

        (await StoredAsync(db)).Should().Be(1);
    }

    [Fact]
    public async Task AddRange_WithoutAGuard_BehavesAsBefore()
    {
        using var db = new SqliteHrmsContext();
        await using (var context = db.NewContext())
        {
            var service = new AttendanceService(new UnitOfWorkService(context));
            await service.AddRangeAsync([Log(LOGSOURCE.MOBILE, 0)]);
            await service.CommitChangesAsync(CancellationToken.None);
        }

        (await StoredAsync(db)).Should().Be(1);
    }
}
