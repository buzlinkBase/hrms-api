using DTR.Core;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;
using Onepunch.Common.Lib.Exceptions;

namespace hrms.test.ServiceTests;

/// <summary>
/// Punch In/Out from My Portal: the log is stored as a PORTAL punch at the server's Manila time, and
/// no two punches may be closer than the company's cooldown (default 5 minutes) -- measured against
/// the employee's latest punch of any source.
/// </summary>
public class PortalPunchServiceTests
{
    // 2026-10-29 00:00 UTC = 08:00 in Manila.
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTime ManilaStart = new(2026, 10, 29, 8, 0, 0);

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static async Task<(SqliteHrmsContext Db, Employee Employee)> SeedAsync(
        int? cooldownMinutes = null, params Attendance[] existing)
    {
        var db = new SqliteHrmsContext();
        var groupId = Guid.NewGuid();
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId, LastName = "Punch", BioId = 7 };
        await using var seed = db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        seed.Employees.Add(employee);
        if (cooldownMinutes is { } minutes)
            seed.GeneralSettings.Add(new GeneralSetting
            {
                IdentityType = PayrollSettingsIdentity.IdentityType,
                Description = PayrollSettingsIdentity.KeyPortalPunchCooldownMinutes,
                Value = minutes.ToString(),
            });
        foreach (var log in existing)
        {
            log.EmployeeId = employee.Id;
            seed.Attendances.Add(log);
        }
        await seed.SaveChangesAsync();
        return (db, employee);
    }

    private static Attendance DeviceLog(DateTime at) =>
        new() { BioId = 7, WorkDateTime = at, LogSource = LOGSOURCE.SYNC, BatchCode = "DEV" };

    private static PortalPunchService ServiceFor(HrmsContext context, TimeProvider clock)
    {
        var uow = new UnitOfWorkService(context);
        return new PortalPunchService(uow, new AttendanceService(uow), new GeneralSettingService(uow), clock);
    }

    private static async Task PunchAsync(SqliteHrmsContext db, Guid employeeId, TimeProvider clock)
    {
        await using var context = db.NewContext();
        var service = ServiceFor(context, clock);
        await service.PunchAsync(employeeId, CancellationToken.None);
        await service.CommitChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Punch_StoresAPortalLog_AtTheManilaTime()
    {
        var (db, employee) = await SeedAsync();
        using var _ = db;

        await PunchAsync(db, employee.Id, new FakeClock(Start));

        await using var verify = db.NewContext();
        var log = await verify.Attendances.SingleAsync();
        log.LogSource.Should().Be(LOGSOURCE.PORTAL);
        log.EmployeeId.Should().Be(employee.Id);
        log.BioId.Should().Be(7);
        log.WorkDateTime.Should().Be(ManilaStart);
    }

    [Fact]
    public async Task ASecondPunchInsideTheCooldown_IsRefused_AndNothingIsStored()
    {
        var (db, employee) = await SeedAsync();
        using var _ = db;
        var clock = new FakeClock(Start);
        await PunchAsync(db, employee.Id, clock);

        clock.Now = Start.AddMinutes(4).AddSeconds(59);
        await using var context = db.NewContext();
        var act = () => ServiceFor(context, clock).PunchAsync(employee.Id, CancellationToken.None);

        await act.Should().ThrowAsync<GuardException>().WithMessage("*wait*");
        await using var verify = db.NewContext();
        (await verify.Attendances.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task APunchAfterTheCooldown_IsAllowed()
    {
        var (db, employee) = await SeedAsync();
        using var _ = db;
        var clock = new FakeClock(Start);
        await PunchAsync(db, employee.Id, clock);

        clock.Now = Start.AddMinutes(5);
        await PunchAsync(db, employee.Id, clock);

        await using var verify = db.NewContext();
        (await verify.Attendances.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ADevicePunchInsideTheCooldown_BlocksThePortalPunch()
    {
        var (db, employee) = await SeedAsync(null, DeviceLog(ManilaStart.AddMinutes(-2)));
        using var _ = db;
        await using var context = db.NewContext();

        var act = () => ServiceFor(context, new FakeClock(Start)).PunchAsync(employee.Id, CancellationToken.None);

        await act.Should().ThrowAsync<GuardException>();
    }

    [Fact]
    public async Task TheCompanysCooldown_IsHonored()
    {
        var (db, employee) = await SeedAsync(cooldownMinutes: 1);
        using var _ = db;
        var clock = new FakeClock(Start);
        await PunchAsync(db, employee.Id, clock);

        clock.Now = Start.AddMinutes(1);
        await PunchAsync(db, employee.Id, clock);

        await using var verify = db.NewContext();
        (await verify.Attendances.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ALongerCooldown_StillBlocksAfterFiveMinutes()
    {
        var (db, employee) = await SeedAsync(cooldownMinutes: 30);
        using var _ = db;
        var clock = new FakeClock(Start);
        await PunchAsync(db, employee.Id, clock);

        clock.Now = Start.AddMinutes(10);
        await using var context = db.NewContext();
        var act = () => ServiceFor(context, clock).PunchAsync(employee.Id, CancellationToken.None);

        await act.Should().ThrowAsync<GuardException>();
    }

    [Fact]
    public async Task APunchDatedAhead_DoesNotBlock()
    {
        var (db, employee) = await SeedAsync(null, DeviceLog(ManilaStart.AddHours(3)));
        using var _ = db;

        await PunchAsync(db, employee.Id, new FakeClock(Start));

        await using var verify = db.NewContext();
        (await verify.Attendances.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Status_ListsTodaysPunches_AndWhenTheNextOneIsAllowed()
    {
        var (db, employee) = await SeedAsync(null, DeviceLog(ManilaStart.AddHours(-30)));
        using var _ = db;
        var clock = new FakeClock(Start);
        await PunchAsync(db, employee.Id, clock);

        clock.Now = Start.AddMinutes(2);
        await using var context = db.NewContext();
        var status = await ServiceFor(context, clock).GetStatusAsync(employee.Id, CancellationToken.None);

        status.CooldownMinutes.Should().Be(5);
        status.LastPunchAt.Should().Be(ManilaStart);
        status.NextAllowedAt.Should().Be(ManilaStart.AddMinutes(5));
        status.Today.Should().ContainSingle().Which.Source.Should().Be(LOGSOURCE.PORTAL);
    }

    [Fact]
    public async Task Status_HasNoWait_WhenTheEmployeeNeverPunched()
    {
        var (db, employee) = await SeedAsync();
        using var _ = db;
        await using var context = db.NewContext();

        var status = await ServiceFor(context, new FakeClock(Start)).GetStatusAsync(employee.Id, CancellationToken.None);

        status.LastPunchAt.Should().BeNull();
        status.NextAllowedAt.Should().BeNull();
        status.Today.Should().BeEmpty();
    }

    [Fact]
    public async Task AnUnknownEmployee_IsRefused()
    {
        var (db, _) = await SeedAsync();
        using var _ = db;
        await using var context = db.NewContext();

        var act = () => ServiceFor(context, new FakeClock(Start)).PunchAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
