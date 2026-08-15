namespace DTR.Core;


public record TimeRange(double TotalMinutes, TimeRecordCollection TimeRecords)
{
    private Dictionary<string, object> Metadata { get; set; } = new();
    public TimeRange(double totalMinutes) : this(totalMinutes, new TimeRecordCollection()) { }
    public TimeRange() : this(0, new TimeRecordCollection()) { }
    public static TimeRange Empty => new TimeRange();
    public static TimeRange Set(double minutes, DateTime startTime, DateTime endTime) => new TimeRange(minutes, TimeRangeSetter.SetTimeRangeCollection(startTime, endTime));
    public static TimeRange Set(double minutes, TimeRecordCollection timeRecords) => new TimeRange(minutes, timeRecords);

    public static TimeRange Set(TimeRecordCollection timeRecords)
    {
        var totalMinutes = timeRecords.TotalMinutes();
        return new TimeRange(totalMinutes, timeRecords);
    }

    // Optional metadata container
    public void SetMetaData<T>(string key, T value) => Metadata[key] = value;
    public T? GetMetaData<T>(string key) => Metadata.TryGetValue(key, out var val) ? (T)val : default;
    public static TimeRange operator +(TimeRange left, TimeRange right)
    {
        if (left.IsEmpty() && right.IsEmpty())
            return TimeRange.Empty;

        if (left.IsEmpty()) return right;
        if (right.IsEmpty()) return left;

        var combinedRecords = new TimeRecordCollection();
        combinedRecords.AddRange(left.TimeRecords);
        combinedRecords.AddRange(right.TimeRecords);

        // Filter out zero-length records 
        var cleanedCollection = combinedRecords
            .MergeOverlapping()
            .ToTimeRecordCollection();
        //generate new timeRange from Collection
        return cleanedCollection.ToTimeRange();
    }
}

public class TimeRecordCollection : List<TimeRecord>
{
    public TimeRecordCollection() { }
    public static TimeRecordCollection Empty => new TimeRecordCollection();
    public TimeRecordCollection(IEnumerable<TimeRecord> records)
        : base(records ?? Enumerable.Empty<TimeRecord>())
    {
    }
}

public class TimeRecord
{
    public TimeRecord()
    {
        StartTime = DateTime.MinValue;
        EndTime = DateTime.MinValue;
    }

    public TimeRecord(DateTime startTime, DateTime endTime, string? tag = "", bool isVirtual = false, bool isLeave = false)
    {
        StartTime = startTime;
        EndTime = endTime;
        Tag = tag ?? string.Empty;
        IsVirtual = isVirtual;
        IsLeave = isLeave;
    }

    public static TimeRecord Set(DateTime startTime, DateTime endTime, string? tag, bool isVirtual = false, bool isLeave = false)
        => new TimeRecord(startTime, endTime, tag, isVirtual, isLeave);

    public static TimeRecord? Null() => null;

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double TotalMinutes => TimeRangeCalculator.GetTotalMinutes(StartTime, EndTime);
    public string Tag { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsLeave { get; set; }
}