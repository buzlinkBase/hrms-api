using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.ServiceTests;

/// <summary>
/// An attendance upload can't be deleted while a saved DTR (any DailyRecord with a BatchCode) covers
/// its logs: per employee, their punch dates in the upload plus the day before the first one. The
/// Upload History list reports the blocking DTR batches. Real SQLite rows, real services.
/// </summary>
public class UploadBatchDtrLockTests
{
    private const string DtrBatch = "DTR-0012";
    private static readonly DateTime Oct1 = new(2026, 10, 1, 8, 0, 0);
    private static readonly DateTime Oct3 = new(2026, 10, 3, 17, 0, 0);
    private static readonly AttendanceLogCaller Hr = new(_ => true, Guid.NewGuid(), "hr@example.com");

    private static async Task<Employee> SeedEmployeeAsync(SqliteHrmsContext db, int bioId = 7)
    {
        await using var seed = db.NewContext();
        var groupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId, BioId = bioId };
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        return employee;
    }

    // An upload "B1" with the employee's punches on Oct 1 and Oct 3.
    private static async Task SeedUploadAsync(SqliteHrmsContext db, Employee employee, string batch = "B1")
    {
        await using var seed = db.NewContext();
        seed.Attendances.AddRange(
            Log(employee, Oct1, batch),
            Log(employee, Oct3, batch));
        await seed.SaveChangesAsync();
    }

    private static Attendance Log(Employee employee, DateTime at, string batch) =>
        new()
        {
            Id = Guid.NewGuid(), BioId = employee.BioId, EmployeeId = employee.Id, WorkDateTime = at,
            LogSource = LOGSOURCE.UPLOADED, BatchCode = batch, DeviceName = "Dat file", EditRemarks = "",
        };

    private static async Task SeedDtrAsync(SqliteHrmsContext db, Guid employeeId, DateOnly date, string? batchCode = DtrBatch)
    {
        await using var seed = db.NewContext();
        seed.DailyTimeRecords.Add(new DailyRecord { Id = Guid.NewGuid(), EmployeeId = employeeId, WorkDate = date, BatchCode = batchCode });
        await seed.SaveChangesAsync();
    }

    private static async Task DeleteUploadAsync(SqliteHrmsContext db, string batch = "B1", IConfidentialAccess? access = null)
    {
        await using var context = access == null ? db.NewContext() : db.NewContext(access);
        var service = new AttendanceService(new UnitOfWorkService(context));
        await service.RemoveUploadBatch(batch, Hr, CancellationToken.None);
        await service.CommitChangesAsync(CancellationToken.None);
    }

    private static async Task<int> LogCountAsync(SqliteHrmsContext db)
    {
        await using var verify = db.NewContext();
        return await verify.Attendances.CountAsync();
    }

    [Fact]
    public async Task SavedDtrOnALogsDate_BlocksTheDelete_NamingTheBatch()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        await SeedUploadAsync(db, employee);
        await SeedDtrAsync(db, employee.Id, new DateOnly(2026, 10, 2));

        var act = () => DeleteUploadAsync(db);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Message.Should().Contain(DtrBatch).And.Contain("Delete that saved DTR first");
        (await LogCountAsync(db)).Should().Be(2);
    }

    [Fact]
    public async Task SavedDtrTheDayBeforeTheFirstLog_Blocks_ANightShiftTimeOut()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        await SeedUploadAsync(db, employee);
        await SeedDtrAsync(db, employee.Id, new DateOnly(2026, 9, 30));

        var act = () => DeleteUploadAsync(db);

        await act.Should().ThrowAsync<ValidationException>();
        (await LogCountAsync(db)).Should().Be(2);
    }

    [Fact]
    public async Task SeveralSavedDtrBatches_AreAllNamed()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        await SeedUploadAsync(db, employee);
        await SeedDtrAsync(db, employee.Id, new DateOnly(2026, 10, 1), "DTR-0002");
        await SeedDtrAsync(db, employee.Id, new DateOnly(2026, 10, 3), "DTR-0001");

        var act = () => DeleteUploadAsync(db);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Message.Should().Contain("DTR batches DTR-0001, DTR-0002");
    }

    [Fact]
    public async Task ACalculatedButUnsavedDtr_DoesNotBlock()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        await SeedUploadAsync(db, employee);
        await SeedDtrAsync(db, employee.Id, new DateOnly(2026, 10, 1), batchCode: null);
        await SeedDtrAsync(db, employee.Id, new DateOnly(2026, 10, 2), batchCode: "");

        await DeleteUploadAsync(db);

        (await LogCountAsync(db)).Should().Be(0);
    }

    [Fact]
    public async Task ASavedDtrOutsideTheUploadsDates_DoesNotBlock()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        await SeedUploadAsync(db, employee);
        await SeedDtrAsync(db, employee.Id, new DateOnly(2026, 9, 29));
        await SeedDtrAsync(db, employee.Id, new DateOnly(2026, 10, 4));

        await DeleteUploadAsync(db);

        (await LogCountAsync(db)).Should().Be(0);
    }

    [Fact]
    public async Task AnotherEmployeesSavedDtr_DoesNotBlock()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        var other = await SeedEmployeeAsync(db, bioId: 8);
        await SeedUploadAsync(db, employee);
        await SeedDtrAsync(db, other.Id, new DateOnly(2026, 10, 1));

        await DeleteUploadAsync(db);

        (await LogCountAsync(db)).Should().Be(0);
    }

    [Fact]
    public async Task AConfidentialEmployeesSavedDtr_StillBlocks_ACallerWhoCantSeeThem()
    {
        using var db = new SqliteHrmsContext();
        var secret = await SeedEmployeeAsync(db);
        await SeedUploadAsync(db, secret);
        await SeedDtrAsync(db, secret.Id, new DateOnly(2026, 10, 1));

        var act = () => DeleteUploadAsync(db, access: new AnalyticsSeed.Uncleared(secret.Id));

        await act.Should().ThrowAsync<ValidationException>().WithMessage($"*{DtrBatch}*");
        (await LogCountAsync(db)).Should().Be(2);
    }

    [Fact]
    public async Task UploadBatches_ReportTheBlockingDtrBatches_OnlyForTheLockedUpload()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        var other = await SeedEmployeeAsync(db, bioId: 8);
        await SeedUploadAsync(db, employee, "LOCKED");
        await SeedUploadAsync(db, other, "FREE");
        await SeedDtrAsync(db, employee.Id, new DateOnly(2026, 10, 3));

        await using var context = db.NewContext();
        var batches = await new AttendanceService(new UnitOfWorkService(context)).GetUploadBatches(
            new UploadBatchFilter { FromDate = new DateOnly(2026, 10, 1), ToDate = new DateOnly(2026, 10, 3) }, CancellationToken.None);

        batches.Single(x => x.BatchCode == "LOCKED").SavedDtrBatches.Should().BeEquivalentTo([DtrBatch]);
        batches.Single(x => x.BatchCode == "FREE").SavedDtrBatches.Should().BeEmpty();
    }
}
