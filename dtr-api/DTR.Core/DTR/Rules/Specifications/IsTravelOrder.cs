namespace DTR.Core;

public class IsTravelOrder : IRuleSpecification
{
    public bool IsSatisfiedBy(TimeRange input, TimeContext context)
    {
        var application = context.Payload.Data.CurrentTravel;
        return application != null;
    }
}
public class IsLeaved  : IRuleSpecification
{
    public bool IsSatisfiedBy(TimeRange input, TimeContext context)
    {
        var application = context.Payload.Data.CurrentLeaves;
        return application != null;
    }
}
