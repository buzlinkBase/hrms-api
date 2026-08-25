using DTR.Core.DTR.Rules.Policies;

namespace DTR.Core;

public class LeaveTimePipeline : IDTRTimePipeline
{
    public TimeRange Apply(TimeContext context, TimeRange input)
    {
        var ledgerKey = TimeRangeLedger.CreateKey("onleave", context);
        var cached = context.Payload.Ledger.GetByKey(ledgerKey);
        if (cached.Found) return cached.Value;
        var pipeline = new LeavePolicy(new IsLeaved());
        var resultRange = pipeline.Apply(input, context);
        context.Payload.Ledger.RecordByTag("onleave", context, resultRange);
        return resultRange;
    }
}
