namespace DTR.Core;

public class LateTimePipeline : IDTRTimePipeline
{
    public TimeRange Apply(TimeContext context, TimeRange cannonicalTimeRange)
    {
        //only get result since pipeline is already run previously in WorkTimePipeline
        return context.Payload.Ledger.GetByTag("late", context);
    }
}
