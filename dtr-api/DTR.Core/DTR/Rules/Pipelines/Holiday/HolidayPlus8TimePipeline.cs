namespace DTR.Core;

public class HolidayPlus8TimePipeline : IDTRTimePipeline
{
    public TimeRange Apply(TimeContext context, TimeRange cannonicalTimeRange)
    {
        var specification = new IsHolidaySpec(HolidayType.LEGAL)
            .And(new IsEligibleForHoliday(HolidayType.LEGAL));

        var holidayKey = TimeRangeLedger.CreateKey<HolidayPlus8TimePipeline>(context);
        var cached = context.Payload.Ledger.GetByKey(holidayKey);
        if (cached.Found)
        {
            return cached.Value ?? TimeRange.Empty;
        }
        var currentDate = context.Payload.Data.CurrentShift.ShiftDate;
        var _holidayType = HolidayType.LEGAL;
        //Get current date holiday only
        var holidays = context.Payload.Provider.HolidayProvider
           .GetHolidayDuringDate(_holidayType, context.Payload.Data.Employee, currentDate)
           ;

        if (!holidays.Any()) return TimeRange.Empty;
        var satisfied = specification.IsSatisfiedBy(cannonicalTimeRange, context);
        if (!satisfied) return TimeRange.Empty;

        var defaultMinutes = satisfied
            ? (context.Payload.Data.CurrentShift.MaxWorkingMinutes)
            : 0;
        var multiplier = holidays.Count();

        //var totalMinutes = defaultMinutes * multiplier;
        //var finalRange = new TimeRange(totalMinutes);
        //var holCreditOption = _context.Payload.Data.CompanyPolicy.HolidayColumnPresentation;
        //if (holCreditOption == HolidayCreditMode.NoCredit)
        //{
        //    finalRange = TimeRange.Empty;
        //}
        var finalRange = TimeRange.Empty;
        finalRange.SetMetaData("HolidayCount", multiplier);
        return finalRange;

    }
}
