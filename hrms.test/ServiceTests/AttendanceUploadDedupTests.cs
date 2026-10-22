using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.ServiceTests;

/// <summary>
/// AttendanceService.AddRangeAsync -- an incoming log replaces a stored one with the same
/// (BioId, EmployeeId, WorkDateTime). Regression: the per-key DELETE loop ran past the client's
/// 30s timeout on a ~1600-row .dat upload in production; the replacement is set-based.
/// </summary>
public class AttendanceUploadDedupTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0);

    private static async Task<(SqliteHrmsContext Db, Employee Employee)> SeedAsync(params Attendance[] existing)
    {
        var db = new SqliteHrmsContext();
        var groupId = Guid.NewGuid();
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId, LastName = "Dela Cruz", BioId = 7 };
        await using var seed = db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        if (existing.Length > 0)
        {
            foreach (var log in existing)
                log.EmployeeId = log.EmployeeId == Guid.Empty ? employee.Id : log.EmployeeId;
            seed.Attendances.AddRange(existing);
            await seed.SaveChangesAsync();
        }
        return (db, employee);
    }

    // EmployeeId = Guid.Empty is a placeholder for "the seeded employee" (resolved in SeedAsync).
    private static Attendance Log(int bioId, DateTime at, string batch, Guid? employeeId) =>
        new() { BioId = bioId, WorkDateTime = at, BatchCode = batch, EmployeeId = employeeId, LogSource = LOGSOURCE.UPLOADED };

    private static async Task UploadAsync(SqliteHrmsContext db, List<Attendance> logs)
    {
        await using var context = db.NewContext();
        var service = new AttendanceService(new UnitOfWorkService(context));
        await service.AddRangeAsync(logs, CancellationToken.None);
        await service.CommitChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ReUploadingTheSamePunches_ReplacesThem_WithoutDuplicates()
    {
        var (db, employee) = await SeedAsync(
            Log(7, Start, "OLD", Guid.Empty),
            Log(7, Start.AddHours(9), "OLD", Guid.Empty));
        using var _ = db;

        await UploadAsync(db, [Log(7, Start, "NEW", employee.Id), Log(7, Start.AddHours(9), "NEW", employee.Id)]);

        await using var verify = db.NewContext();
        var logs = await verify.Attendances.ToListAsync();
        logs.Should().HaveCount(2);
        logs.Should().OnlyContain(x => x.BatchCode == "NEW");
    }

    [Fact]
    public async Task LogsWithADifferentTimeOrEmployee_AreLeftAlone()
    {
        var (db, employee) = await SeedAsync(
            Log(7, Start.AddMinutes(1), "KEEP-TIME", Guid.Empty),
            Log(7, Start, "KEEP-UNREGISTERED", null));
        using var _ = db;

        await UploadAsync(db, [Log(7, Start, "NEW", employee.Id)]);

        await using var verify = db.NewContext();
        var batches = await verify.Attendances.Select(x => x.BatchCode).ToListAsync();
        batches.Should().BeEquivalentTo(["KEEP-TIME", "KEEP-UNREGISTERED", "NEW"]);
    }

    [Fact]
    public async Task AnUnregisteredPunch_IsReplacedOnReUpload()
    {
        var (db, _) = await SeedAsync(Log(99, Start, "OLD", null));
        using var _ = db;

        await UploadAsync(db, [Log(99, Start, "NEW", null)]);

        await using var verify = db.NewContext();
        var log = await verify.Attendances.SingleAsync();
        log.BatchCode.Should().Be("NEW");
        log.EmployeeId.Should().BeNull();
    }

    private static async Task ChangeAsync(SqliteHrmsContext db, Func<AttendanceService, Task> act)
    {
        await using var context = db.NewContext();
        var service = new AttendanceService(new UnitOfWorkService(context));
        await act(service);
        await service.CommitChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ReUploadingARemovedPunch_DoesNotBringItBack()
    {
        var removed = Log(7, Start, "OLD", Guid.Empty);
        var (db, employee) = await SeedAsync(removed, Log(7, Start.AddHours(9), "OLD", Guid.Empty));
        using var _ = db;
        await ChangeAsync(db, s => s.Remove(removed.Id, AttendanceLogCaller.System, "duplicate", CancellationToken.None));

        await UploadAsync(db, [Log(7, Start, "NEW", employee.Id), Log(7, Start.AddHours(9), "NEW", employee.Id)]);

        await using var verify = db.NewContext();
        var logs = await verify.Attendances.ToListAsync();
        logs.Should().ContainSingle().Which.WorkDateTime.Should().Be(Start.AddHours(9));
    }

    [Fact]
    public async Task ReSyncingAnEditedPunch_KeepsTheEdit_WithoutADuplicateAtTheOriginalTime()
    {
        var edited = Log(7, Start, "OLD", Guid.Empty);
        edited.LogSource = LOGSOURCE.SYNC;
        var (db, employee) = await SeedAsync(edited);
        using var _ = db;
        await ChangeAsync(db, s => s.Update(
            new UpdateAttendance { Id = edited.Id, WorkTime = Start.AddMinutes(30), Remarks = "device clock was off" },
            AttendanceLogCaller.System, CancellationToken.None));

        var resync = Log(7, Start, "NEW", employee.Id);
        resync.LogSource = LOGSOURCE.SYNC;
        await UploadAsync(db, [resync]);

        await using var verify = db.NewContext();
        var log = await verify.Attendances.SingleAsync();
        log.Id.Should().Be(edited.Id);
        log.WorkDateTime.Should().Be(Start.AddMinutes(30));
    }

    [Fact]
    public async Task AnIncomingPunchAtAnEditedLogsNewTime_DoesNotReplaceTheEditedLog()
    {
        var edited = Log(7, Start, "OLD", Guid.Empty);
        var (db, employee) = await SeedAsync(edited);
        using var _ = db;
        await ChangeAsync(db, s => s.Update(
            new UpdateAttendance { Id = edited.Id, WorkTime = Start.AddMinutes(30), Remarks = "fixed" },
            AttendanceLogCaller.System, CancellationToken.None));

        await UploadAsync(db, [Log(7, Start.AddMinutes(30), "NEW", employee.Id)]);

        await using var verify = db.NewContext();
        var log = await verify.Attendances.SingleAsync();
        log.Id.Should().Be(edited.Id);
        log.EditRemarks.Should().Be("fixed");
    }

    [Fact]
    public async Task AManualEntryAtARemovedPunchsTime_IsStillAdded()
    {
        var removed = Log(7, Start, "OLD", Guid.Empty);
        var (db, employee) = await SeedAsync(removed);
        using var _ = db;
        await ChangeAsync(db, s => s.Remove(removed.Id, AttendanceLogCaller.System, null, CancellationToken.None));

        var manual = Log(7, Start, "MANUAL-1", employee.Id);
        manual.LogSource = LOGSOURCE.MANUAL;
        await UploadAsync(db, [manual]);

        await using var verify = db.NewContext();
        (await verify.Attendances.SingleAsync()).LogSource.Should().Be(LOGSOURCE.MANUAL);
    }

    [Fact]
    public async Task AfterTheUploadBatchIsDeleted_ReUploadingTheFile_ImportsEverythingAgain()
    {
        var removed = Log(7, Start, "OLD", Guid.Empty);
        var edited = Log(7, Start.AddHours(9), "OLD", Guid.Empty);
        var (db, employee) = await SeedAsync(removed, edited);
        using var _ = db;
        await ChangeAsync(db, s => s.Remove(removed.Id, AttendanceLogCaller.System, null, CancellationToken.None));
        await ChangeAsync(db, s => s.Update(
            new UpdateAttendance { Id = edited.Id, WorkTime = Start.AddHours(10), Remarks = "fixed" },
            AttendanceLogCaller.System, CancellationToken.None));

        await ChangeAsync(db, s => s.RemoveUploadBatch("OLD", AttendanceLogCaller.System, CancellationToken.None));
        await UploadAsync(db, [Log(7, Start, "NEW", employee.Id), Log(7, Start.AddHours(9), "NEW", employee.Id)]);

        await using var verify = db.NewContext();
        (await verify.Attendances.Select(x => x.WorkDateTime).ToListAsync())
            .Should().BeEquivalentTo([Start, Start.AddHours(9)]);
        (await verify.AttendanceOriginalLogs.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ABatchLargerThanOneDeleteChunk_ReplacesEveryDuplicate()
    {
        const int count = 1500;
        var (db, employee) = await SeedAsync(
            Enumerable.Range(0, count).Select(i => Log(7, Start.AddMinutes(i), "OLD", Guid.Empty)).ToArray());
        using var _ = db;

        await UploadAsync(db, Enumerable.Range(0, count).Select(i => Log(7, Start.AddMinutes(i), "NEW", employee.Id)).ToList());

        await using var verify = db.NewContext();
        (await verify.Attendances.CountAsync()).Should().Be(count);
        (await verify.Attendances.AnyAsync(x => x.BatchCode == "OLD")).Should().BeFalse();
    }
}
