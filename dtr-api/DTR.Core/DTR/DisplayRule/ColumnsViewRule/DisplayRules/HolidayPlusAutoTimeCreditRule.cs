using DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;

namespace DTR.Core.DTR.DisplayRule.ColumnsViewRule.DisplayRules;

internal class HolidayPlusAutoTimeCreditRule : IColumnDisplayRule
{
    public HolidayPlusAutoTimeCreditRule()
    {
    }
    public TimeRange ApplyRules(DisplayContext context)
    {
        if (context.TimeContext.IsRestDay()) return TimeRange.Empty; 
        var Plus8 = context.PipeLineResult.Plus8;
        var holOption = new IsShowWorkOnHolidayInRegColumn().IsSatisfiedBy(context);
        if (!holOption)
        {
            var holiday = context.PipeLineResult.LegalHoliday;
            return new TimeRange(Plus8.TotalMinutes + holiday.TotalMinutes,  holiday.TimeRecords);
        }
        return Plus8;
    }
}