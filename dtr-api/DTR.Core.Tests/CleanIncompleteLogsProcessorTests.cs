using DTR.Core.Tests.TestSupport;
using Hrms.Domain.Entities;

namespace DTR.Core.Tests;

/// <summary>
/// CleanIncompleteLogsProcessor -- the rows behind the admin Incomplete Logs screen and the
/// portal's My Incomplete Logs: an unpaired punch, or a past workday with no punch at all.
/// </summary>
public class CleanIncompleteLogsProcessorTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 5, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 5, 17, 0, 0);
    private static readonly DateOnly Day = new(2026, 1, 5);
    private static readonly CleanIncompleteLogsProcessor Processor = new(Day.AddDays(1));

    [Fact]
    public void UnpairedPunch_IsListedWithItsPunch()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        ApplyAttendance(context, ShiftStart);

        var row = Processor.Process(context.Payload);

        row.Should().NotBeNull();
        row!.EmployeeId.Should().Be(context.Payload.Data.Employee.Id);
        row.Log1!.WorkTime.Should().Be(ShiftStart);
        row.Log2.Should().BeNull();
    }

    [Fact]
    public void PairedPunches_AreNotListed()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        ApplyAttendance(context, ShiftStart, ShiftEnd);

        Processor.Process(context.Payload).Should().BeNull();
    }

    // Regression: a workday with no punch at all came back as an empty placeholder row, which
    // the screens discard, so days the employee never logged were never flagged.
    [Fact]
    public void PastWorkdayWithNoPunches_IsListedWithNoLogs()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);

        var row = Processor.Process(context.Payload);

        row.Should().NotBeNull();
        row!.EmployeeId.Should().Be(context.Payload.Data.Employee.Id);
        row.WorkDate.Should().Be(Day);
        row.Log1.Should().BeNull();
    }

    [Fact]
    public void TodayOrLaterWithNoPunches_IsNotListedYet()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);

        new CleanIncompleteLogsProcessor(Day).Process(context.Payload).Should().BeNull();
        new CleanIncompleteLogsProcessor(Day.AddDays(-1)).Process(context.Payload).Should().BeNull();
    }

    [Fact]
    public void RestDayWithNoPunches_IsNotListed()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        MarkAsRestDay(context);

        Processor.Process(context.Payload).Should().BeNull();
    }

    [Fact]
    public void LegalHolidayWithNoPunches_IsNotListed()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, Day));
        var context = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);

        Processor.Process(context.Payload).Should().BeNull();
    }

    [Fact]
    public void LeaveDayWithNoPunches_IsNotListed()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        context.Payload.Data.CurrentLeaves =
        [
            new LeaveApplication
            {
                Leave = new Leave { PaySource = PaySource.Company },
                PayType = PayType.WithoutPay,
                DurationType = DurationType.SingleDay,
                LeaveDateFrom = Day,
                LeaveDateTo = Day,
            },
        ];

        Processor.Process(context.Payload).Should().BeNull();
    }
}
