namespace Hrms.Core.Services;

// Philippine (Asia/Manila) calendar days for screens that filter by "today" or "the last N days".
// Audit timestamps (CreatedAt) are stamped in UTC but read back from MySQL with Kind=Unspecified,
// so day boundaries are converted to UTC before filtering and values are re-marked UTC on output.
public static class PhTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

    public static DateOnly Today(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utcNow), Zone));

    // PH midnight of the given date, as a UTC instant comparable with UTC CreatedAt columns.
    public static DateTime StartOfDayUtc(DateOnly phDate) =>
        TimeZoneInfo.ConvertTimeToUtc(phDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zone);

    // Marks a stored UTC value as UTC so it serializes with a trailing "Z".
    public static DateTime AsUtc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);
}
