namespace DTR.Core;

public class OverTimePipeline : IDTRTimePipeline
{
    public TimeRange Apply(TimeContext context, TimeRange cannonicalTimeRange)
    {
        var ledger = context.Payload.Ledger;
        var key = TimeRangeLedger.CreateKey("FinalOT", context);
        var cached = context.Payload.Ledger.GetByKey(key);
        if (cached.Found)
        {
            return cached.Value ?? TimeRange.Empty;
        }
        if (IsRequire8HourWork(context))
        {
            var workTime = context.Payload.Ledger.GetByTag("work_time", context);
            var reach8hr = workTime.TotalMinutes >= context.Payload.Data.CurrentShift.MaxWorkingMinutes;
            if (!reach8hr)
            {
                return TimeRange.Empty;
            }
        }

        //get actual OT
        return new OverTimeHandlerProcessor(context).Handle(cannonicalTimeRange);
    }

    private bool IsRequire8HourWork(TimeContext context)
    {
        var restrictOt = context.Payload.Data.CompanyPolicy.OTEligibility == OvertimeEligibilityRule.RequireFullRegularHours;
        if (context.Payload.Data.Employee.ClientId.HasValue)
        {
            var clientPolicy = context.Payload.Provider.ClientPolicyProvider
                .GetPolicy(context.Payload.Data.Employee.ClientId);
            if (clientPolicy != null)
            {
                restrictOt = clientPolicy.OvertimeEligibilityRule == OvertimeEligibilityRule.RequireFullRegularHours;
            }
        }
        return restrictOt;
    }
}
