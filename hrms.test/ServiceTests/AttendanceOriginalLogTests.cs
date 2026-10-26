using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.ServiceTests;

/// <summary>
/// AttendanceOriginalLog -- the saved original of an uploaded/synced log a user edited or removed:
/// kept on the first edit, carried into a removal, restored, and cleared with its upload batch.
/// </summary>
public class AttendanceOriginalLogTests
{
    private static readonly DateTime At = new(2026, 10, 1, 8, 0, 0);
    private static readonly AttendanceLogCaller Hr = new(_ => true, Guid.NewGuid(), "hr@example.com");

    private static async Task<(SqliteHrmsContext Db, Guid EmployeeId)> SeedAsync(params Attendance[] logs)
    {
        var db = new SqliteHrmsContext();
        var groupId = Guid.NewGuid();
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId, LastName = "Dela Cruz", BioId = 7 };
        await using var seed = db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        seed.Employees.Add(employee);
        foreach (var log in logs) log.EmployeeId ??= employee.Id;
        seed.Attendances.AddRange(logs);
        await seed.SaveChangesAsync();
        return (db, employee.Id);
    }

    private static Attendance Log(LOGSOURCE source, DateTime at, string batch = "B1") =>
        new() { Id = Guid.NewGuid(), BioId = 7, WorkDateTime = at, LogSource = source, BatchCode = batch, DeviceName = "Gate", EditRemarks = "" };

    private static async Task RunAsync(SqliteHrmsContext db, Func<AttendanceService, Task> act)
    {
        await using var context = db.NewContext();
        var service = new AttendanceService(new UnitOfWorkService(context));
        await act(service);
        await service.CommitChangesAsync(CancellationToken.None);
    }

    private static UpdateAttendance MoveTo(Guid id, DateTime at, string remarks = "wrong punch") =>
        new() { Id = id, WorkTime = at, Remarks = remarks };

    [Fact]
    public async Task TheFirstEdit_SavesTheOriginal_ALaterEditKeepsIt()
    {
        var log = Log(LOGSOURCE.SYNC, At);
        var (db, _) = await SeedAsync(log);
        using var _db = db;

        await RunAsync(db, s => s.Update(MoveTo(log.Id, At.AddMinutes(5), "first"), Hr, CancellationToken.None));
        await RunAsync(db, s => s.Update(MoveTo(log.Id, At.AddMinutes(9), "second"), Hr, CancellationToken.None));

        await using var verify = db.NewContext();
        var original = await verify.AttendanceOriginalLogs.SingleAsync();
        original.Action.Should().Be(ATTENDANCE_ORIGINAL_ACTION.EDITED);
        original.AttendanceId.Should().Be(log.Id);
        original.WorkDateTime.Should().Be(At);
        original.DeviceName.Should().Be("Gate");
        original.Reason.Should().Be("first");
        original.ActedByName.Should().Be("hr@example.com");
        (await verify.Attendances.SingleAsync()).WorkDateTime.Should().Be(At.AddMinutes(9));
    }

    [Fact]
    public async Task EditingAManualLog_SavesNoOriginal()
    {
        var log = Log(LOGSOURCE.MANUAL, At);
        var (db, _) = await SeedAsync(log);
        using var _db = db;

        await RunAsync(db, s => s.Update(MoveTo(log.Id, At.AddMinutes(5)), Hr, CancellationToken.None));
        await RunAsync(db, s => s.Remove(log.Id, Hr, null, CancellationToken.None));

        await using var verify = db.NewContext();
        (await verify.AttendanceOriginalLogs.AnyAsync()).Should().BeFalse();
        (await verify.Attendances.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task RemovingALog_HardDeletesIt_AndSavesTheOriginal()
    {
        var log = Log(LOGSOURCE.UPLOADED, At);
        var (db, _) = await SeedAsync(log);
        using var _db = db;

        await RunAsync(db, s => s.Remove(log.Id, Hr, "duplicate punch", CancellationToken.None));

        await using var verify = db.NewContext();
        (await verify.Attendances.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
        var original = await verify.AttendanceOriginalLogs.SingleAsync();
        original.Action.Should().Be(ATTENDANCE_ORIGINAL_ACTION.DELETED);
        original.AttendanceId.Should().BeNull();
        original.WorkDateTime.Should().Be(At);
        original.Reason.Should().Be("duplicate punch");
    }

    [Fact]
    public async Task RemovingAnEditedLog_KeepsTheTrueOriginal_AsTheRemovedOne()
    {
        var log = Log(LOGSOURCE.SYNC, At);
        var (db, _) = await SeedAsync(log);
        using var _db = db;

        await RunAsync(db, s => s.Update(MoveTo(log.Id, At.AddMinutes(5)), Hr, CancellationToken.None));
        await RunAsync(db, s => s.Remove(log.Id, Hr, "not theirs", CancellationToken.None));

        await using var verify = db.NewContext();
        var original = await verify.AttendanceOriginalLogs.SingleAsync();
        original.Action.Should().Be(ATTENDANCE_ORIGINAL_ACTION.DELETED);
        original.AttendanceId.Should().BeNull();
        original.WorkDateTime.Should().Be(At);
        original.Reason.Should().Be("not theirs");
    }

    [Fact]
    public async Task RestoringAnEdit_PutsBackTheOriginalTimeAndRemarks_AndDropsTheOriginal()
    {
        var log = Log(LOGSOURCE.UPLOADED, At);
        var (db, _) = await SeedAsync(log);
        using var _db = db;
        await RunAsync(db, s => s.Update(MoveTo(log.Id, At.AddMinutes(5)), Hr, CancellationToken.None));
        Guid originalId;
        await using (var read = db.NewContext())
            originalId = (await read.AttendanceOriginalLogs.SingleAsync()).Id;

        await RunAsync(db, s => s.RestoreOriginal(originalId, Hr, CancellationToken.None));

        await using var verify = db.NewContext();
        var restored = await verify.Attendances.SingleAsync();
        restored.Id.Should().Be(log.Id);
        restored.WorkDateTime.Should().Be(At);
        restored.EditRemarks.Should().BeEmpty();
        (await verify.AttendanceOriginalLogs.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task RestoringARemoval_RecreatesTheLog_AndDropsTheOriginal()
    {
        var log = Log(LOGSOURCE.SYNC, At);
        var (db, employeeId) = await SeedAsync(log);
        using var _db = db;
        await RunAsync(db, s => s.Remove(log.Id, Hr, null, CancellationToken.None));
        Guid originalId;
        await using (var read = db.NewContext())
            originalId = (await read.AttendanceOriginalLogs.SingleAsync()).Id;

        await RunAsync(db, s => s.RestoreOriginal(originalId, Hr, CancellationToken.None));

        await using var verify = db.NewContext();
        var restored = await verify.Attendances.SingleAsync();
        restored.WorkDateTime.Should().Be(At);
        restored.LogSource.Should().Be(LOGSOURCE.SYNC);
        restored.EmployeeId.Should().Be(employeeId);
        restored.BatchCode.Should().Be("B1");
        restored.DeviceName.Should().Be("Gate");
        (await verify.AttendanceOriginalLogs.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task DeletingAnUploadBatch_RemovesItsLogsAndOriginals_OnlyThatBatch()
    {
        var kept = Log(LOGSOURCE.UPLOADED, At, "B1");
        var edited = Log(LOGSOURCE.UPLOADED, At.AddHours(1), "B1");
        var removed = Log(LOGSOURCE.UPLOADED, At.AddHours(2), "B1");
        var otherUpload = Log(LOGSOURCE.UPLOADED, At.AddHours(3), "B2");
        var synced = Log(LOGSOURCE.SYNC, At.AddHours(4), "B1");
        var (db, _) = await SeedAsync(kept, edited, removed, otherUpload, synced);
        using var _db = db;
        await RunAsync(db, s => s.Update(MoveTo(edited.Id, At.AddHours(1).AddMinutes(5)), Hr, CancellationToken.None));
        await RunAsync(db, s => s.Remove(removed.Id, Hr, null, CancellationToken.None));
        await RunAsync(db, s => s.Remove(synced.Id, Hr, null, CancellationToken.None));

        await RunAsync(db, s => s.RemoveUploadBatch("B1", Hr, CancellationToken.None));

        await using var verify = db.NewContext();
        (await verify.Attendances.Select(x => x.Id).ToListAsync()).Should().BeEquivalentTo([otherUpload.Id]);
        // The removed synced log's original stays: it isn't part of the upload.
        (await verify.AttendanceOriginalLogs.Select(x => x.LogSource).ToListAsync()).Should().BeEquivalentTo([LOGSOURCE.SYNC]);
    }

    [Fact]
    public async Task UploadBatches_ListsEachUploadWhole_IncludingOneWhoseLogsWereAllRemoved()
    {
        var a1 = Log(LOGSOURCE.UPLOADED, At, "A");
        var a2 = Log(LOGSOURCE.UPLOADED, At.AddDays(5), "A");
        var gone = Log(LOGSOURCE.UPLOADED, At, "GONE");
        var (db, _) = await SeedAsync(a1, a2, gone, Log(LOGSOURCE.SYNC, At, "S"), Log(LOGSOURCE.MANUAL, At, "M"));
        using var _db = db;
        await RunAsync(db, s => s.Update(MoveTo(a1.Id, At.AddMinutes(5)), Hr, CancellationToken.None));
        await RunAsync(db, s => s.Remove(gone.Id, Hr, null, CancellationToken.None));

        List<UploadBatchModel> batches = [];
        await RunAsync(db, async s => batches = await s.GetUploadBatches(
            new UploadBatchFilter { FromDate = DateOnly.FromDateTime(At), ToDate = DateOnly.FromDateTime(At) }, CancellationToken.None));

        batches.Select(x => x.BatchCode).Should().BeEquivalentTo(["A", "GONE"]);
        var a = batches.Single(x => x.BatchCode == "A");
        a.LogCount.Should().Be(2);
        a.EmployeeCount.Should().Be(1);
        a.ChangedCount.Should().Be(1);
        a.LastLog.Should().Be(At.AddDays(5));
        var allRemoved = batches.Single(x => x.BatchCode == "GONE");
        allRemoved.LogCount.Should().Be(0);
        allRemoved.ChangedCount.Should().Be(1);
    }

    [Fact]
    public async Task UploadBatchChanges_ListsEditedAndRemovedLogs_OfThatUploadOnly()
    {
        var edited = Log(LOGSOURCE.UPLOADED, At, "A");
        var removed = Log(LOGSOURCE.UPLOADED, At.AddHours(1), "A");
        var untouched = Log(LOGSOURCE.UPLOADED, At.AddHours(2), "A");
        var otherUpload = Log(LOGSOURCE.UPLOADED, At, "B");
        var (db, _) = await SeedAsync(edited, removed, untouched, otherUpload);
        using var _db = db;
        await RunAsync(db, s => s.Update(MoveTo(edited.Id, At.AddMinutes(5), "late punch"), Hr, CancellationToken.None));
        await RunAsync(db, s => s.Remove(removed.Id, Hr, "duplicate", CancellationToken.None));
        await RunAsync(db, s => s.Update(MoveTo(otherUpload.Id, At.AddMinutes(9)), Hr, CancellationToken.None));

        List<UploadBatchChangeModel> changes = [];
        await RunAsync(db, async s => changes = await s.GetUploadBatchChanges("A", CancellationToken.None));

        changes.Should().HaveCount(2);
        var edit = changes.Single(x => x.Action == "EDITED");
        edit.OriginalDateTime.Should().Be(At);
        edit.CurrentDateTime.Should().Be(At.AddMinutes(5));
        edit.Reason.Should().Be("late punch");
        edit.EmployeeName.Should().StartWith("Dela Cruz");
        edit.ChangedBy.Should().Be("hr@example.com");
        var removal = changes.Single(x => x.Action == "DELETED");
        removal.OriginalDateTime.Should().Be(At.AddHours(1));
        removal.CurrentDateTime.Should().BeNull();
        removal.Reason.Should().Be("duplicate");
    }
}
