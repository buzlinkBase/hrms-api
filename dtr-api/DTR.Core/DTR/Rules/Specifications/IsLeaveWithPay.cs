namespace DTR.Core;

public class IsLeaveWithPay : IRuleSpecification
{
    private readonly DateOnly _curDate;
    public IsLeaveWithPay(DateOnly curDate)
    {
        _curDate = curDate;
    }
    public bool IsSatisfiedBy(TimeRange input, TimeContext context)
    {
        //check if employee is on leave with pay
        var currentLeaves = context.Payload.Provider.LeaveProvider.GetApplications(_curDate);
        return currentLeaves != null && currentLeaves.Any(x => x.PayType == PayType.WithPay);
    }
}
