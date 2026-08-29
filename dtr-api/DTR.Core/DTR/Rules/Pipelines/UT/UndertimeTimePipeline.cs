namespace DTR.Core;

public class UndertimeTimePipeline : IDTRTimePipeline
{
    public TimeRange Apply(TimeContext context, TimeRange cannonicalTimeRange)
    {
        var applied = new AppliedUnderTimeHandler(cannonicalTimeRange, context);
        var actual = new ActualUnderTimeHandler(cannonicalTimeRange, context);
        applied.SetNext(actual);
        return applied.Handle();
    }
}
