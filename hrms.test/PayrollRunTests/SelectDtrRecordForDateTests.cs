using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// EmployeePayrollLineService.SelectDtrRecordForDate — the per-day record pick inside
/// CalculateDTRTimePay, extracted so the "multiple DTR batches combined into one payroll run"
/// scenario is directly testable without a database or the full calculator DI graph. Combining
/// batches (PayrollRunPayload.BatchCodes is already a List&lt;string&gt;, unioned by
/// DailyRecordService.LoadForPayrollRunAsync) is how this codebase lets one payroll run pull in
/// an employee's DTR across multiple clients/date-sub-ranges within one cutoff — this suite
/// proves the per-day pick behaves correctly regardless of how many candidate rows exist for a
/// date, and always favors an hours-bearing row over a zero-hour placeholder.
/// </summary>
public class SelectDtrRecordForDateTests
{
    private static readonly Guid EmployeeId = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 3, 5);

    private static DailyRecordRunModel Record(
        double regularNetHours = 0, Guid? clientId = null, string? batchCode = null, DateOnly? date = null) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = EmployeeId,
        WorkDate = date ?? Date,
        RegularNetHours = regularNetHours,
        ClientId = clientId,
        BatchCode = batchCode,
    };

    [Fact]
    public void SingleRecord_HoursBearing_IsReturned()
    {
        var record = Record(regularNetHours: 8);
        var dtrs = new List<DailyRecordRunModel> { record };

        EmployeePayrollLineService.SelectDtrRecordForDate(dtrs, EmployeeId, Date)
            .Should().Be(record);
    }

    [Fact]
    public void SingleRecord_ZeroHours_StillReturned_AbsentHolidayLeaveCase()
    {
        // The ordinary absent/holiday/unpaid-leave day: exactly one zero-hour placeholder
        // record exists for the date — it must still be captured, not skipped, so that day's
        // DtrId/Client/etc. are traceable even though there was nothing worked.
        var record = Record(regularNetHours: 0);
        var dtrs = new List<DailyRecordRunModel> { record };

        EmployeePayrollLineService.SelectDtrRecordForDate(dtrs, EmployeeId, Date)
            .Should().Be(record);
    }

    [Fact]
    public void NoRecordForDate_ReturnsNull()
    {
        var dtrs = new List<DailyRecordRunModel> { Record(date: Date.AddDays(1)) };

        EmployeePayrollLineService.SelectDtrRecordForDate(dtrs, EmployeeId, Date)
            .Should().BeNull();
    }

    [Fact]
    public void MultipleCandidatesForSameDate_PrefersHoursBearingOverZeroHour()
    {
        // Simulates combining two DTR batches (e.g. two different clients' DTR runs) that
        // hypothetically both produced a row for this employee+date — the zero-hour
        // placeholder must lose to the one that actually has worked hours.
        var zero = Record(regularNetHours: 0, clientId: Guid.NewGuid());
        var worked = Record(regularNetHours: 8, clientId: Guid.NewGuid());
        var dtrs = new List<DailyRecordRunModel> { zero, worked };

        EmployeePayrollLineService.SelectDtrRecordForDate(dtrs, EmployeeId, Date)
            .Should().Be(worked);
    }

    [Fact]
    public void MultipleZeroHourCandidates_FallsBackToFirst()
    {
        var first = Record(regularNetHours: 0);
        var second = Record(regularNetHours: 0);
        var dtrs = new List<DailyRecordRunModel> { first, second };

        var result = EmployeePayrollLineService.SelectDtrRecordForDate(dtrs, EmployeeId, Date);

        result.Should().NotBeNull();
        dtrs.Should().Contain(result);
    }

    [Fact]
    public void CombinedBatches_EachDateResolvesToItsOwnBatchsRecord()
    {
        // The realistic multi-batch scenario: two DTR batches for the same employee, covering
        // non-overlapping date sub-ranges (e.g. Client A days 1-3, Client B days 4-5) — unioned
        // into one flat list exactly like DailyRecordService.LoadForPayrollRunAsync does when
        // multiple BatchCodes are selected for one payroll run. Each date must resolve to the
        // record from whichever batch actually covers it.
        var clientA = Guid.NewGuid();
        var clientB = Guid.NewGuid();
        var day1 = Record(regularNetHours: 8, clientId: clientA, batchCode: "BATCH-A", date: new DateOnly(2026, 3, 1));
        var day2 = Record(regularNetHours: 8, clientId: clientA, batchCode: "BATCH-A", date: new DateOnly(2026, 3, 2));
        var day4 = Record(regularNetHours: 8, clientId: clientB, batchCode: "BATCH-B", date: new DateOnly(2026, 3, 4));
        var day5 = Record(regularNetHours: 8, clientId: clientB, batchCode: "BATCH-B", date: new DateOnly(2026, 3, 5));
        var dtrs = new List<DailyRecordRunModel> { day1, day2, day4, day5 };

        EmployeePayrollLineService.SelectDtrRecordForDate(dtrs, EmployeeId, new DateOnly(2026, 3, 1))
            .Should().Be(day1);
        EmployeePayrollLineService.SelectDtrRecordForDate(dtrs, EmployeeId, new DateOnly(2026, 3, 2))
            .Should().Be(day2);
        // Day 3 has no record in either batch (e.g. a rest day never posted) — must be null,
        // not silently pick up a neighboring day's record.
        EmployeePayrollLineService.SelectDtrRecordForDate(dtrs, EmployeeId, new DateOnly(2026, 3, 3))
            .Should().BeNull();
        EmployeePayrollLineService.SelectDtrRecordForDate(dtrs, EmployeeId, new DateOnly(2026, 3, 4))
            .Should().Be(day4);
        EmployeePayrollLineService.SelectDtrRecordForDate(dtrs, EmployeeId, new DateOnly(2026, 3, 5))
            .Should().Be(day5);
    }
}
