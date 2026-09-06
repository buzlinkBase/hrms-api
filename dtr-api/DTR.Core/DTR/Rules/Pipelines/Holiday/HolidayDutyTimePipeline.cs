namespace DTR.Core;

public class HolidayDutyTimePipeline : IHolidayDutyTimePipeline
{
    public TimeRange Apply(TimeContext context, HolidayType holidayType, TimeRange cannonicalTimeRange)
    {
        var spec = new IsHolidaySpec(holidayType)
            .And(new IsEligibleWorkHours());

        var pipeLine = new WorkTimePipeline(context, spec)
            .Apply(cannonicalTimeRange);

        var holidayRange = context.Payload.Ledger.GetByTag($"holiday_portion_{holidayType}", context);

        if (holidayType == HolidayType.SPECIAL)
        {
            var holidays = context.Payload.Provider.HolidayProvider
                .GetHolidayDuringDate(holidayType, context.Payload.Data.Employee, context.Payload.Data.CurrentDate);

            var multiplier = holidays.Count();
            holidayRange.SetMetaData("SPHolidayCount", multiplier);
        }
        return holidayRange;

    }
}
