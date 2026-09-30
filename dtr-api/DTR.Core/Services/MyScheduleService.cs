using Hrms.Domain.Entities;

namespace DTR.Core;

/// <summary>
/// An employee's own schedule, date by date, exactly as the DTR will compute it: the same
/// RosterReportService the Rostering report uses, which composes the DTR's own chains --
/// Work Rotation Plan &gt; Fixed Schedule &gt; Permanent Shift &gt; Open Shift for the shift, and
/// Change Rest Day &gt; Rest Day Date &gt; weekly rest days for rest days. Nothing is re-derived
/// here, so My Shift Schedule can never disagree with the DTR.
/// </summary>
public class MyScheduleService
{
    public const int DefaultDays = 14;
    public const int MaxDays = 62;

    // Same zone the Dashboard and DTR use for "today" -- a schedule is about the local day,
    // which in Manila starts 8 hours before the UTC one.
    private static readonly TimeZoneInfo LocalZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

    private readonly RosterReportService _roster;

    public MyScheduleService(RosterReportService roster) => _roster = roster;

    public static DateOnly LocalToday(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), LocalZone));

    /// <summary>
    /// The dates to show: from <paramref name="today"/> for two weeks unless given, never
    /// backwards, and at most <see cref="MaxDays"/> days.
    /// </summary>
    public static (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var start = from ?? today;
        var end = to ?? start.AddDays(DefaultDays - 1);
        if (end < start) (start, end) = (end, start);
        var last = start.AddDays(MaxDays - 1);
        return (start, end > last ? last : end);
    }

    public Task<List<RosterReportModel>> GetAsync(
        Guid employeeId, DateOnly? from, DateOnly? to, DateOnly today, CancellationToken token)
    {
        var (start, end) = Range(from, to, today);
        return _roster.RosterReportQuery(
            new DTRRequestPayload { EmployeeId = employeeId, FromDate = start, ToDate = end },
            token);
    }
}
