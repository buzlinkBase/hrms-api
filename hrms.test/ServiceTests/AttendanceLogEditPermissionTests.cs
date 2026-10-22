using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;
using Onepunch.Common.Lib.Exceptions;

namespace hrms.test.ServiceTests;

/// <summary>
/// AttendanceLogSourcePolicy through AttendanceService -- editing, removing and restoring a log
/// needs the permission for its own source (manual, uploaded, synced); other sources can't be
/// changed at all.
/// </summary>
public class AttendanceLogEditPermissionTests
{
    private static readonly DateTime At = new(2026, 10, 1, 8, 0, 0);

    private static readonly string[] AllCodes =
    [
        AttendanceLogPermissions.Edit, AttendanceLogPermissions.Delete,
        AttendanceLogPermissions.EditUploaded, AttendanceLogPermissions.DeleteUploaded,
        AttendanceLogPermissions.EditSynced, AttendanceLogPermissions.DeleteSynced,
    ];

    private static AttendanceLogCaller Holding(params string[] codes) => new(codes.Contains, Guid.NewGuid(), "hr@example.com");

    private static AttendanceLogCaller HoldingAllBut(string code) => Holding(AllCodes.Where(x => x != code).ToArray());

    private static async Task<(SqliteHrmsContext Db, Attendance Log)> SeedAsync(LOGSOURCE source)
    {
        var db = new SqliteHrmsContext();
        var groupId = Guid.NewGuid();
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId, LastName = "Dela Cruz", BioId = 7 };
        var log = new Attendance { Id = Guid.NewGuid(), BioId = 7, EmployeeId = employee.Id, WorkDateTime = At, LogSource = source, BatchCode = "B1" };
        await using var seed = db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        seed.Employees.Add(employee);
        seed.Attendances.Add(log);
        await seed.SaveChangesAsync();
        return (db, log);
    }

    private static async Task RunAsync(SqliteHrmsContext db, Func<AttendanceService, Task> act)
    {
        await using var context = db.NewContext();
        var service = new AttendanceService(new UnitOfWorkService(context));
        await act(service);
        await service.CommitChangesAsync(CancellationToken.None);
    }

    private static UpdateAttendance MoveTo(Guid id, DateTime at) => new() { Id = id, WorkTime = at, Remarks = "wrong punch" };

    [Theory]
    [InlineData(LOGSOURCE.MANUAL, AttendanceLogPermissions.Edit)]
    [InlineData(LOGSOURCE.UPLOADED, AttendanceLogPermissions.EditUploaded)]
    [InlineData(LOGSOURCE.SYNC, AttendanceLogPermissions.EditSynced)]
    public async Task Update_IsAllowed_WithTheEditPermissionOfTheLogsSource(LOGSOURCE source, string code)
    {
        var (db, log) = await SeedAsync(source);
        using var _ = db;

        await RunAsync(db, s => s.Update(MoveTo(log.Id, At.AddMinutes(5)), Holding(code), CancellationToken.None));

        await using var verify = db.NewContext();
        (await verify.Attendances.SingleAsync()).WorkDateTime.Should().Be(At.AddMinutes(5));
    }

    [Theory]
    [InlineData(LOGSOURCE.MANUAL, AttendanceLogPermissions.Edit)]
    [InlineData(LOGSOURCE.UPLOADED, AttendanceLogPermissions.EditUploaded)]
    [InlineData(LOGSOURCE.SYNC, AttendanceLogPermissions.EditSynced)]
    public async Task Update_IsForbidden_WithoutTheEditPermissionOfTheLogsSource(LOGSOURCE source, string code)
    {
        var (db, log) = await SeedAsync(source);
        using var _ = db;

        var act = () => RunAsync(db, s => s.Update(MoveTo(log.Id, At.AddMinutes(5)), HoldingAllBut(code), CancellationToken.None));

        await act.Should().ThrowAsync<ForbiddenException>();
        await using var verify = db.NewContext();
        (await verify.Attendances.SingleAsync()).WorkDateTime.Should().Be(At);
        (await verify.AttendanceOriginalLogs.AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData(LOGSOURCE.MANUAL, AttendanceLogPermissions.Delete)]
    [InlineData(LOGSOURCE.UPLOADED, AttendanceLogPermissions.DeleteUploaded)]
    [InlineData(LOGSOURCE.SYNC, AttendanceLogPermissions.DeleteSynced)]
    public async Task Remove_IsAllowed_WithTheDeletePermissionOfTheLogsSource(LOGSOURCE source, string code)
    {
        var (db, log) = await SeedAsync(source);
        using var _ = db;

        await RunAsync(db, s => s.Remove(log.Id, Holding(code), null, CancellationToken.None));

        await using var verify = db.NewContext();
        (await verify.Attendances.AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData(LOGSOURCE.MANUAL, AttendanceLogPermissions.Delete)]
    [InlineData(LOGSOURCE.UPLOADED, AttendanceLogPermissions.DeleteUploaded)]
    [InlineData(LOGSOURCE.SYNC, AttendanceLogPermissions.DeleteSynced)]
    public async Task Remove_IsForbidden_WithoutTheDeletePermissionOfTheLogsSource(LOGSOURCE source, string code)
    {
        var (db, log) = await SeedAsync(source);
        using var _ = db;

        var act = () => RunAsync(db, s => s.Remove(log.Id, HoldingAllBut(code), null, CancellationToken.None));

        await act.Should().ThrowAsync<ForbiddenException>();
        await using var verify = db.NewContext();
        (await verify.Attendances.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(LOGSOURCE.BIOMETRIC)]
    [InlineData(LOGSOURCE.MOBILE)]
    [InlineData(LOGSOURCE.GEOFENCE)]
    [InlineData(LOGSOURCE.OTHER)]
    public async Task OtherSources_CantBeEditedOrRemoved_EvenWithEveryPermission(LOGSOURCE source)
    {
        var (db, log) = await SeedAsync(source);
        using var _ = db;

        var edit = () => RunAsync(db, s => s.Update(MoveTo(log.Id, At.AddMinutes(5)), Holding(AllCodes), CancellationToken.None));
        var remove = () => RunAsync(db, s => s.Remove(log.Id, Holding(AllCodes), null, CancellationToken.None));

        await edit.Should().ThrowAsync<ValidationException>();
        await remove.Should().ThrowAsync<ValidationException>();
        await using var verify = db.NewContext();
        (await verify.Attendances.SingleAsync()).WorkDateTime.Should().Be(At);
    }

    [Fact]
    public async Task Update_OfAMissingLog_IsNotFound()
    {
        var (db, _) = await SeedAsync(LOGSOURCE.MANUAL);
        using var _db = db;

        var act = () => RunAsync(db, s => s.Update(MoveTo(Guid.NewGuid(), At), Holding(AllCodes), CancellationToken.None));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RemoveUploadBatch_NeedsDeleteUploaded()
    {
        var (db, _) = await SeedAsync(LOGSOURCE.UPLOADED);
        using var _db = db;

        var act = () => RunAsync(db, s => s.RemoveUploadBatch("B1", HoldingAllBut(AttendanceLogPermissions.DeleteUploaded), CancellationToken.None));

        await act.Should().ThrowAsync<ForbiddenException>();
        await using var verify = db.NewContext();
        (await verify.Attendances.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RestoringAnEdit_NeedsTheSourcesEditPermission_RestoringARemoval_ItsDeletePermission()
    {
        var (db, log) = await SeedAsync(LOGSOURCE.SYNC);
        using var _db = db;
        await RunAsync(db, s => s.Update(MoveTo(log.Id, At.AddMinutes(5)), Holding(AllCodes), CancellationToken.None));
        Guid editedOriginal;
        await using (var read = db.NewContext())
            editedOriginal = (await read.AttendanceOriginalLogs.SingleAsync()).Id;

        var restoreEditWithoutEdit = () => RunAsync(db, s => s.RestoreOriginal(editedOriginal, HoldingAllBut(AttendanceLogPermissions.EditSynced), CancellationToken.None));
        await restoreEditWithoutEdit.Should().ThrowAsync<ForbiddenException>();

        await RunAsync(db, s => s.Remove(log.Id, Holding(AllCodes), "duplicate", CancellationToken.None));
        var restoreRemovalWithoutDelete = () => RunAsync(db, s => s.RestoreOriginal(editedOriginal, HoldingAllBut(AttendanceLogPermissions.DeleteSynced), CancellationToken.None));
        await restoreRemovalWithoutDelete.Should().ThrowAsync<ForbiddenException>();

        await RunAsync(db, s => s.RestoreOriginal(editedOriginal, Holding(AttendanceLogPermissions.DeleteSynced), CancellationToken.None));
        await using var verify = db.NewContext();
        (await verify.Attendances.SingleAsync()).WorkDateTime.Should().Be(At);
    }
}
