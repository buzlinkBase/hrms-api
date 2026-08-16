namespace DTR.Core.DTR.Rules.Policies;

public class LeavePolicy : ConditionalPolicyBase
{
    public LeavePolicy(IRuleSpecification spec) : base(spec, SpecFailureBehavior.ReturnEmpty) { }

    protected override TimeRange ApplyIfSatisfied(TimeRange input, TimeContext context)
    {
        var application = context.Payload.Data.CurrentLeave;
        if (application == null) return TimeRange.Empty;

        var strategy = LeaveTimeRangeStrategyFactory.Create(application);
        return strategy.ComputeTimeRange(application, context);
    }
}
