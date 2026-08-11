using DTR.Core.DTR.Rules.Policies;
using Elastic.Clients.Elasticsearch.MachineLearning;

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
        var ledgerKey = TimeRangeLedger.CreateKey("travel", _context);
        var cached = _context.Payload.Ledger.GetByKey(ledgerKey);
        if (cached.Found)
        {
            return cached.Value;
        }
        var pipeline = new TravelPolicy(new IsTravelOrder());
        var resultRange = pipeline.Apply(cannonicalTimeRange, _context);
        _context.Payload.Ledger.RecordByTag("travel", _context, resultRange);
        return resultRange;
    }
}