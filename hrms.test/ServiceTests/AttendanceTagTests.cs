using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.ServiceTests;

/// <summary>
/// AttendanceService.Tag -- assigning Unregistered Employees logs to an employee by Bio ID -- and
/// RemoveBatch, the Attendance Manual Entry batch delete.
/// </summary>
public class AttendanceTagTests
{
    [Fact]
    public async Task RemoveBatch_DeletesOnlyManualLogs()
    {
        using var db = new SqliteHrmsContext();
        var day = new DateTime(2026, 3, 2, 8, 0, 0);
        var manual = new Attendance { Id = Guid.NewGuid(), BatchCode = "B1", LogSource = LOGSOURCE.MANUAL, WorkDateTime = day };
        var uploaded = new Attendance { Id = Guid.NewGuid(), BatchCode = "B1", LogSource = LOGSOURCE.UPLOADED, WorkDateTime = day };
        var synced = new Attendance { Id = Guid.NewGuid(), BatchCode = "B1", LogSource = LOGSOURCE.SYNC, WorkDateTime = day };
        await using (var seed = db.NewContext())
        {
            seed.Attendances.AddRange(manual, uploaded, synced);
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
        {
            // As AttendanceController's batch delete does: remove, then commit.
            var service = new AttendanceService(new UnitOfWorkService(context));
            await service.RemoveBatch("B1", CancellationToken.None);
            await service.CommitChangesAsync(CancellationToken.None);
        }

        await using var verify = db.NewContext();
        (await verify.Attendances.Select(x => x.Id).ToListAsync())
            .Should().BeEquivalentTo([uploaded.Id, synced.Id]);
    }

    // Regression: tagging re-assigned every log with the Bio ID, so a former employee who held it
    // before it was reassigned lost their attendance history to the new holder.
    [Fact]
    public async Task Tag_AssignsOnlyUnregisteredLogs_LeavingAnotherEmployeesLogsAlone()
    {
        using var db = new SqliteHrmsContext();
        var groupId = Guid.NewGuid();
        var former = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId, LastName = "Former", BioId = 7 };
        var current = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId, LastName = "Current", BioId = 7 };
        var formerLog = new Attendance { Id = Guid.NewGuid(), BioId = 7, EmployeeId = former.Id, WorkDateTime = new DateTime(2025, 6, 2, 8, 0, 0) };
        var unregistered = new Attendance { Id = Guid.NewGuid(), BioId = 7, WorkDateTime = new DateTime(2026, 3, 2, 8, 0, 0) };
        await using (var seed = db.NewContext())
        {
            seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
            seed.Employees.AddRange(former, current);
            seed.Attendances.AddRange(formerLog, unregistered);
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
            await new AttendanceService(new UnitOfWorkService(context)).Tag(unregistered, current, CancellationToken.None);

        await using var verify = db.NewContext();
        (await verify.Attendances.SingleAsync(x => x.Id == unregistered.Id)).EmployeeId.Should().Be(current.Id);
        (await verify.Attendances.SingleAsync(x => x.Id == formerLog.Id)).EmployeeId.Should().Be(former.Id);
    }
}
