using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;

namespace hrms.test.ServiceTests;

/// <summary>
/// AttendanceService.GetAllLogsInRange — the DTR "Attendance Logs" view. It must list exactly the
/// logs the DTR captures for the shift (AttendanceCaptureWindow): from the earliest allowed
/// clock-in (company TimeInAllowance, default -120) up to, not including, the next shift's
/// earliest allowed clock-in. Midday shift 2026-09-19 12:00-20:00, next shift 2026-09-20 07:00.
/// </summary>
public class AttendanceShiftLogsTests
{
    private static readonly DateTime ShiftStart = new(2026, 9, 19, 12, 0, 0);
    private static readonly DateTime NextShiftStart = new(2026, 9, 20, 7, 0, 0);

    private static CurrentShiftInfo Current(TimeShiftType type = TimeShiftType.FIXED) =>
        new() { ShiftType = type, StartTime = ShiftStart, EndTime = new DateTime(2026, 9, 19, 20, 0, 0) };

    private static readonly CurrentShiftInfo Next =
        new() { ShiftType = TimeShiftType.FIXED, StartTime = NextShiftStart, EndTime = new DateTime(2026, 9, 20, 15, 0, 0) };

    private static async Task<Guid> SeedLogs(SqliteHrmsContext db, string? timeInAllowance, params DateTime[] times)
    {
        var groupId = Guid.NewGuid();
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId, LastName = "Employee" };
        var other = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId, LastName = "Other" };
        await using var seed = db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        seed.Employees.AddRange(employee, other);
        seed.Attendances.AddRange(times.Select(t => new Attendance
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, WorkDateTime = t, LogSource = LOGSOURCE.UPLOADED,
        }));
        // Another employee's log inside the same window never shows up.
        seed.Attendances.Add(new Attendance { Id = Guid.NewGuid(), EmployeeId = other.Id, WorkDateTime = ShiftStart, LogSource = LOGSOURCE.UPLOADED });
        if (timeInAllowance != null)
        {
            seed.GeneralSettings.Add(new GeneralSetting
            {
                Id = Guid.NewGuid(), IdentityType = "Company", Description = nameof(SettingKey.TimeInAllowance), Value = timeInAllowance,
            });
        }
        await seed.SaveChangesAsync();
        return employee.Id;
    }

    private static async Task<List<DateTime>> LogsInRange(SqliteHrmsContext db, Guid employeeId, CurrentShiftInfo current)
    {
        await using var context = db.NewContext();
        var logs = await new AttendanceService(new UnitOfWorkService(context))
            .GetAllLogsInRange(employeeId, current, Next, CancellationToken.None);
        return logs.Select(x => x.WorkDateTime).ToList();
    }

    // Regression: the list started at the shift start, so a time-in the DTR counted (e.g. 10:30
    // for a 12:00 shift, inside the 2h earliest allowed clock-in) was missing from the view.
    [Fact]
    public async Task EarlyClockInWithinTheDefaultAllowance_IsListed_LikeTheDTRCapturesIt()
    {
        using var db = new SqliteHrmsContext();
        var earlyIn = ShiftStart.AddMinutes(-90); // 10:30
        var out1 = new DateTime(2026, 9, 20, 0, 7, 37);
        var out2 = new DateTime(2026, 9, 20, 0, 7, 39);
        var employeeId = await SeedLogs(db, null,
            ShiftStart.AddMinutes(-121), // 09:59, before the earliest allowed clock-in
            earlyIn, out1, out2,
            NextShiftStart.AddMinutes(-120)); // 05:00, the next shift's earliest allowed clock-in

        (await LogsInRange(db, employeeId, Current())).Should().Equal(earlyIn, out1, out2);
    }

    [Fact]
    public async Task CompanyTimeInAllowanceSetting_IsUsed()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedLogs(db, "-60",
            ShiftStart.AddMinutes(-61),
            ShiftStart.AddMinutes(-60),
            NextShiftStart.AddMinutes(-61),
            NextShiftStart.AddMinutes(-60));

        (await LogsInRange(db, employeeId, Current()))
            .Should().Equal(ShiftStart.AddMinutes(-60), NextShiftStart.AddMinutes(-61));
    }

    [Fact]
    public async Task SplitShift_HasNoClockInAllowance()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = await SeedLogs(db, null,
            ShiftStart.AddMinutes(-1),
            ShiftStart,
            NextShiftStart.AddMinutes(-1),
            NextShiftStart);

        (await LogsInRange(db, employeeId, Current(TimeShiftType.SPLIT)))
            .Should().Equal(ShiftStart, NextShiftStart.AddMinutes(-1));
    }
}
