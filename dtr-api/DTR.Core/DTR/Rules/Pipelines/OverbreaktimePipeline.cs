namespace DTR.Core;

public class OverbreaktimePipeline : IDTRTimePipeline
{
    public TimeRange Apply(TimeContext context, TimeRange cannonicalTimeRange)
    {

        var spec = new IsOverbreaktimeSpec();
        //.And(new IsFixedScheduleSpec());

        var overBreakPolicy = new OverBreakPolicy(spec);

        var pipeline = new PolicyPipeline()
          .AddPolicy(overBreakPolicy);

        var result = pipeline.Execute(cannonicalTimeRange, context);

        return result;
    }
}
