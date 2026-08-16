using DTR.Core.DTR.Rules.Policies;

namespace DTR.Core;

public class LeaveTimePipeline
{
    private readonly TimeContext _context;
    public LeaveTimePipeline(TimeContext context)
    {
        _context = context;
    }
    public TimeRange Apply(TimeRange input)
    {

        var ledgerKey = TimeRangeLedger.CreateKey("onleave", _context);
        var cached = _context.Payload.Ledger.GetByKey(ledgerKey);
        if (cached.Found)
        {
            return cached.Value;
        }
        var pipeline = new LeavePolicy(new IsLeaved());
        var resultRange = pipeline.Apply(input, _context);
        _context.Payload.Ledger.RecordByTag("onleave", _context, resultRange);
        return resultRange;

    }
}