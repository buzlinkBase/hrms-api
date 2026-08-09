namespace DTR.Core;

public class TravelPipeline
{
    private readonly TimeContext _context;
    public TravelPipeline(TimeContext context)
    {
        _context = context;
    }
    public TimeRange Apply(TimeRange cannonicalTimeRange)
    {
        var spec = new IsTravelOrder()
            .IsSatisfiedBy(cannonicalTimeRange, _context);
        if (!spec) return TimeRange.Empty;

        var ledgerKey = TimeRangeLedger.CreateKey<TravelPipeline>(_context);
        var cached = _context.Payload.Ledger.GetByKey(ledgerKey);
        if (cached.Found)
        {
            return cached.Value;
        }

        var application = _context.Payload.Data.CurrentTravel;
        if (application == null) return TimeRange.Empty;

        if (application.IsManualEntry)
        {
            var resultRange = new TimeRange(application.TotalMinutes);
            _context.Payload.Ledger.RecordByTag("travel", _context, resultRange);
            return resultRange;
        }

        if (!application.StartTime.HasValue || !application.EndTime.HasValue)
        {
            _context.Payload.Ledger.RecordByTag("travel", _context, TimeRange.Empty);
            return TimeRange.Empty;
        }

        var raw = cannonicalTimeRange.TimeRecords;
        var blocked = _context.Payload.Ledger
            .GetAllAllocatedExcept(ledgerKey)
            .MergeOverlapping();

        var timeBlock = new TimeRecordCollection()
        {
            new TimeRecord
            {
                StartTime=application.StartTime.Value,
                EndTime=application.EndTime.Value,
            }
        };

        //cap to timeshift
        var capped = timeBlock
            .CapAndCrop(_context.Payload.Data.CurrentShift);

        var rangeResult = capped.TimeRecords
            .Exclude(blocked)
            .ToTimeRange();

        _context.Payload.Ledger.RecordByTag("travel", _context, rangeResult);
        return rangeResult;

    }
}