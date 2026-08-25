using DTR.Core.DTR.Rules.Policies;

namespace DTR.Core;

public class TravelPipeline : IDTRTimePipeline
{
    public TimeRange Apply(TimeContext context, TimeRange cannonicalTimeRange)
    {
        var ledgerKey = TimeRangeLedger.CreateKey("travel", context);
        var cached = context.Payload.Ledger.GetByKey(ledgerKey);
        if (cached.Found)
        {
            return cached.Value;
        }
        var pipeline = new TravelPolicy(new IsTravelOrder());
        var resultRange = pipeline.Apply(cannonicalTimeRange, context);
        context.Payload.Ledger.RecordByTag("travel", context, resultRange);
        return resultRange;
    }
}
