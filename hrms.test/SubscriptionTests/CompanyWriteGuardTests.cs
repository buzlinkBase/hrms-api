using hrms.test.TestSupport;
using Hrms.Core.Messaging;
using Hrms.Core.Services;
using Hrms.Core.Services.Subscriptions;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Onepunch.Common.Lib.Subscriptions;

namespace hrms.test.SubscriptionTests;

/// <summary>
/// A read-only company can't change attendance, DTR or payroll runs from any path -- not only
/// through the HTTP middleware, but from queued work (the DTR Save Draft worker, the attendance
/// queue) too. Device punches keep being stored through the lapse-retention window.
/// </summary>
public class CompanyWriteGuardTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 2, 0, 0, DateTimeKind.Utc);

    private static TimeProvider Clock()
    {
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(new DateTimeOffset(Now));
        return clock;
    }

    private static CompanyWriteGuard GuardFor(EntitlementSnapshot snapshot, ITenantProvider? tenant = null) =>
        new(Snapshots.Provider(snapshot), tenant ?? Snapshots.Tenant(), Clock());

    private static ICompanyWriteGuard ReadOnlyGuard() => GuardFor(Snapshots.With(SubscriptionAccessMode.ReadOnly));

    private static EntitlementSnapshot ReadOnlyWithPunchesUntil(DateTime until) =>
        Snapshots.With(SubscriptionAccessMode.ReadOnly) with { PunchesAcceptedUntilUtc = until };

    // ── The guard ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FullAccess_Passes()
    {
        var act = () => GuardFor(Snapshots.With()).EnsureWritableAsync(default);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadOnly_IsRefused_AsReadOnly()
    {
        var act = () => ReadOnlyGuard().EnsureWritableAsync(default);

        (await act.Should().ThrowAsync<PlanRestrictionException>()).Which.ProblemType
            .Should().Be(SubscriptionProblemTypes.ReadOnly);
    }

    [Fact]
    public async Task StoppedByAnOperator_IsRefused_AsLocked()
    {
        var blocked = Snapshots.With() with { Restriction = new AccessRestriction(AccessRestrictionKinds.Suspended, null) };

        var act = () => GuardFor(blocked).EnsureWritableAsync(default);

        (await act.Should().ThrowAsync<PlanRestrictionException>()).Which.ProblemType
            .Should().Be(SubscriptionProblemTypes.Locked);
    }

    [Fact]
    public async Task NoCompany_IsASystemCall_AndPasses_WithoutALookup()
    {
        var tenant = Substitute.For<ITenantProvider>();
        tenant.TenantId.Returns(Guid.Empty);
        var provider = Substitute.For<IEntitlementProvider>();

        await new CompanyWriteGuard(provider, tenant, Clock()).EnsureWritableAsync(default);

        await provider.DidNotReceiveWithAnyArgs().GetAsync(default, default);
    }

    [Theory]
    [InlineData(LOGSOURCE.SYNC)]
    [InlineData(LOGSOURCE.BIOMETRIC)]
    public async Task DevicePunches_AreStored_WithinTheRetentionWindow(LOGSOURCE source)
    {
        var act = () => GuardFor(ReadOnlyWithPunchesUntil(Now.AddDays(1)))
            .EnsureCanStoreAttendanceAsync([source], default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DevicePunches_AreRefused_PastTheRetentionWindow()
    {
        var act = () => GuardFor(ReadOnlyWithPunchesUntil(Now.AddDays(-1)))
            .EnsureCanStoreAttendanceAsync([LOGSOURCE.SYNC], default);

        await act.Should().ThrowAsync<PlanRestrictionException>();
    }

    [Theory]
    [InlineData(LOGSOURCE.MANUAL)]
    [InlineData(LOGSOURCE.UPLOADED)]
    [InlineData(LOGSOURCE.MOBILE)]
    [InlineData(LOGSOURCE.PORTAL)]
    public async Task OtherAttendance_IsRefused_WhileReadOnly_EvenWithinTheRetentionWindow(LOGSOURCE source)
    {
        var act = () => GuardFor(ReadOnlyWithPunchesUntil(Now.AddDays(1)))
            .EnsureCanStoreAttendanceAsync([source], default);

        (await act.Should().ThrowAsync<PlanRestrictionException>()).Which.ProblemType
            .Should().Be(SubscriptionProblemTypes.ReadOnly);
    }

    [Fact]
    public async Task ABatchMixingDeviceAndManualLogs_IsRefused_WhileReadOnly()
    {
        var act = () => GuardFor(ReadOnlyWithPunchesUntil(Now.AddDays(1)))
            .EnsureCanStoreAttendanceAsync([LOGSOURCE.SYNC, LOGSOURCE.MANUAL], default);

        await act.Should().ThrowAsync<PlanRestrictionException>();
    }

    // ── Attendance: nothing is stored when refused ───────────────────────────────────────────

    private static Attendance Log(LOGSOURCE source, int minute) => new()
    {
        Id = Guid.NewGuid(),
        BioId = 7,
        LogSource = source,
        WorkDateTime = new DateTime(2026, 10, 28, 8, minute, 0),
    };

    private static async Task<int> StoredAttendanceAsync(SqliteHrmsContext db)
    {
        await using var verify = db.NewContext();
        return await verify.Attendances.CountAsync();
    }

    [Fact]
    public async Task Attendance_ManualLogs_AreNotStored_WhileReadOnly()
    {
        using var db = new SqliteHrmsContext();
        await using (var context = db.NewContext())
        {
            var service = new AttendanceService(new UnitOfWorkService(context), writeGuard: ReadOnlyGuard());

            var act = () => service.AddRangeAsync([Log(LOGSOURCE.MANUAL, 0)]);

            await act.Should().ThrowAsync<PlanRestrictionException>();
            await service.CommitChangesAsync(CancellationToken.None);
        }

        (await StoredAttendanceAsync(db)).Should().Be(0);
    }

    [Fact]
    public async Task Attendance_SyncedPunches_AreStillStored_WithinTheRetentionWindow()
    {
        using var db = new SqliteHrmsContext();
        await using (var context = db.NewContext())
        {
            var guard = GuardFor(ReadOnlyWithPunchesUntil(Now.AddDays(1)));
            var service = new AttendanceService(new UnitOfWorkService(context), writeGuard: guard);
            await service.AddRangeAsync([Log(LOGSOURCE.SYNC, 0)]);
            await service.CommitChangesAsync(CancellationToken.None);
        }

        (await StoredAttendanceAsync(db)).Should().Be(1);
    }

    [Fact]
    public async Task Attendance_RemovingABatch_IsRefused_WhileReadOnly()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var service = new AttendanceService(new UnitOfWorkService(context), writeGuard: ReadOnlyGuard());

        var act = () => service.RemoveBatch("BATCH-20261028-001", default);

        await act.Should().ThrowAsync<PlanRestrictionException>();
    }

    // ── DTR and payroll runs: refused before any work is done ────────────────────────────────
    // The guard is the first thing each entry point asks, so the other collaborators are never
    // reached (null here on purpose: touching one would fail the test differently).

    [Fact]
    public async Task Dtr_QueuedSaveDraft_IsRefused_WhenTheWorkerGetsToIt()
    {
        var service = new DtrDraftSaveService(null!, null!, null!, null!, null!, ReadOnlyGuard());

        var act = () => service.SaveAsync(new SaveDtrDraftRequested(), default);

        await act.Should().ThrowAsync<PlanRestrictionException>();
    }

    [Fact]
    public async Task Dtr_SaveDraft_IsRefused_WhenQueued()
    {
        var service = new DtrDraftSaveService(null!, null!, null!, null!, null!, ReadOnlyGuard());

        var act = () => service.QueueAsync(new SaveDtrDraftRequest(), Guid.NewGuid(), Guid.NewGuid(), default);

        await act.Should().ThrowAsync<PlanRestrictionException>();
    }

    [Fact]
    public async Task Dtr_ApprovingABatch_IsRefused_WhileReadOnly()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var service = new DailyRecordService(new UnitOfWorkService(context), null!, null!, null!, null!, null!, null!, null!,
            writeGuard: ReadOnlyGuard());

        var act = () => service.ApproveBatchAsync(Guid.NewGuid(), Guid.NewGuid(), approverHasOverride: true, null, default);

        await act.Should().ThrowAsync<PlanRestrictionException>();
    }

    [Fact]
    public async Task Payroll_Generate_IsRefused_WhileReadOnly()
    {
        var service = new PayrollProcessorService(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, null!, null!, null!, null!, ReadOnlyGuard());

        var act = () => service.GenerateAsync(new PayrollRunPayload(), Guid.NewGuid(), Guid.NewGuid(), default);

        await act.Should().ThrowAsync<PlanRestrictionException>();
    }

    [Fact]
    public async Task Payroll_DeletingABatch_IsRefused_WhileReadOnly()
    {
        var service = new PayrollBatchLifecycleService(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, ReadOnlyGuard());

        var act = () => service.DeleteBatchAsync(Guid.NewGuid(), default);

        await act.Should().ThrowAsync<PlanRestrictionException>();
    }

    [Fact]
    public async Task Payroll_SavingRows_IsRefused_WhileReadOnly_AndNothingIsStored()
    {
        using var db = new SqliteHrmsContext();
        await using (var context = db.NewContext())
        {
            var service = new PayrollService(new UnitOfWorkService(context), Substitute.For<IMapper>(), ReadOnlyGuard());

            var act = () => service.SavePayrollsAsync([new Payroll { Id = Guid.NewGuid(), EmployeeId = Guid.NewGuid() }], default);

            await act.Should().ThrowAsync<PlanRestrictionException>();
        }

        await using var verify = db.NewContext();
        (await verify.Payrolls.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Payroll_Calculate_StaysOpen_AsAPreview()
    {
        // Only Generate and the batch actions are guarded: the preview never asks the guard.
        var guard = Substitute.For<ICompanyWriteGuard>();
        var service = new PayrollProcessorService(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, null!, null!, null!, null!, guard);

        try { await service.CalculateAsync(new PayrollRunPayload(), Guid.NewGuid(), default); }
        catch (Exception) { /* its collaborators are null here -- only the guard matters */ }

        await guard.DidNotReceiveWithAnyArgs().EnsureWritableAsync(default);
    }
}
