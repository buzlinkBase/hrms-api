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

public class IsGovFundedLeaved : IRuleSpecification
{
    public bool IsSatisfiedBy(TimeRange input, TimeContext context)
    {
        var hasSpecialLeave = context.Payload.Data.CurrentLeaves
           .Where(x => (x.Leave.PaySource == PaySource.Government ||
               x.Leave.PaySource == PaySource.Shared) && 
               x.PayoutMode == PayoutMode.OneTime &&
               x.PayType == PayType.WithPay 
               )
           .Any();

        return hasSpecialLeave;
    }
}
