using Hrms.Domain.Entities;

namespace DTR.Core;
public class TimeRangeSetter
{
    public static TimeRecordCollection SetTimeRangeCollection(TimeRecord range)
    {
        return new TimeRecordCollection { range };
    }
    public static TimeRecordCollection SetTimeRangeCollection(DateTime startDateTime, DateTime endDateTime)
    {
        return SetTimeRangeCollection(new TimeRecord { StartTime = startDateTime, EndTime = endDateTime });
    }
    public static TimeRecordCollection SetTimeRangeCollection(List<Attendance> paired)
    {
        TimeRecordCollection timeRecords = new TimeRecordCollection();
        for (int i = 0; i < paired.Count - 1; i += 2)
        {
            if (i + 1 < paired.Count && paired[i + 1] == null) continue;
            timeRecords.Add(new TimeRecord
            {
                StartTime = paired[i].WorkDateTime,
                EndTime = paired[i + 1].WorkDateTime
            });
        }
        return timeRecords;
    }
}
