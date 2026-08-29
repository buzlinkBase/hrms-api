namespace DTR.Core;

public class NonHolidayDutyTimePipeline : IDTRTimePipeline
{
    public TimeRange Apply(TimeContext context, TimeRange cannonicalTimeRange)
    {
        var isHoliday = new IsHolidaySpec(HolidayType.LEGAL)
        .Or(new IsHolidaySpec(HolidayType.SPECIAL));

        var spec = new IsEligibleWorkHours();
        var workRange = new WorkTimePipeline(context, spec)
            .Apply(cannonicalTimeRange);


        if (isHoliday.IsSatisfiedBy(cannonicalTimeRange, context))
        {
            //if holiday return only nonholiday
            //we may also check the   new IsHolTimeInDayType() here
            var regKey = TimeRangeLedger.CreateKey("non_holiday_portion", context);
            var cached = context.Payload.Ledger.GetByKey(regKey);
            if (cached.Found)
            {
                var value = cached.Value ?? TimeRange.Empty;
                context.Payload.Ledger.RecordByTag("final_RegularTime", context, value);
                return value;
            }
            else
            {
                return TimeRange.Empty;
            }
        }
        //non holiday
        context.Payload.Ledger.RecordByTag("final_RegularTime", context, workRange);
        return workRange;
    }
}
