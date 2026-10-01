using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.ServiceTests;

/// <summary>AttendanceService.AddManualEntriesAsync -- Attendance Manual Entry and the DTR's "Add time log".</summary>
public class ManualAttendanceEntryTests
{
    private static readonly DateTime Punch = new(2026, 9, 12, 8, 0, 0);

    private static async Task<(SqliteHrmsContext Db, Employee Visible, Employee Secret, Guid Branch)> SeedAsync()
    {
        var db = new SqliteHrmsContext();
        var groupId = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var visible = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId, LastName = "Visible", BioId = 1 };
        var secret = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId, LastName = "Secret", BioId = 2, IsConfidential = true };
        await using var seed = db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        seed.Branches.Add(new Branch { Id = branch, Name = "Cebu" });
        seed.Employees.AddRange(visible, secret);
        await seed.SaveChangesAsync();
        return (db, visible, secret, branch);
    }

    private static CreateAttendance Entry(Guid employeeId, Guid? branchId = null) =>
        new() { EmployeeId = employeeId, WorkTime = Punch, Remarks = "Forgot to punch", BranchId = branchId };

    [Fact]
    public async Task SavesTheEntry_AtTheChosenBranch()
    {
        var (db, visible, _, branch) = await SeedAsync();
        using var _ = db;

        await using (var context = db.NewContext())
        {
            var service = new AttendanceService(new UnitOfWorkService(context));
            await service.AddManualEntriesAsync([Entry(visible.Id, branch)], CancellationToken.None);
            await service.CommitChangesAsync(CancellationToken.None);
        }

        await using var verify = db.NewContext();
        var log = await verify.Attendances.SingleAsync();
        log.EmployeeId.Should().Be(visible.Id);
        log.BranchId.Should().Be(branch);
        log.LogSource.Should().Be(LOGSOURCE.MANUAL);
    }

    // Regression: an employee the caller can't see was skipped while the request still answered
    // "success", so the entry silently never saved.
    [Fact]
    public async Task AnEmployeeTheCallerCantSee_IsRefused_NotSilentlySkipped()
    {
        var (db, visible, secret, _) = await SeedAsync();
        using var _ = db;
        await using var context = db.NewContext(new AnalyticsSeed.Uncleared(secret.Id));
        var service = new AttendanceService(new UnitOfWorkService(context));

        var act = () => service.AddManualEntriesAsync([Entry(visible.Id), Entry(secret.Id)], CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*access*");
    }

    [Fact]
    public async Task AnUnknownBranch_IsRefused()
    {
        var (db, visible, _, _) = await SeedAsync();
        using var _ = db;
        await using var context = db.NewContext();
        var service = new AttendanceService(new UnitOfWorkService(context));

        var act = () => service.AddManualEntriesAsync([Entry(visible.Id, Guid.NewGuid())], CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*branch*");
    }

    [Fact]
    public async Task AnEntryWithoutRemarks_IsRefused()
    {
        var (db, visible, _, _) = await SeedAsync();
        using var _ = db;
        await using var context = db.NewContext();
        var service = new AttendanceService(new UnitOfWorkService(context));

        var act = () => service.AddManualEntriesAsync(
            [new CreateAttendance { EmployeeId = visible.Id, WorkTime = Punch, Remarks = " " }], CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Remarks*");
    }
}
