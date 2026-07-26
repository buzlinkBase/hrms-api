using DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;

namespace DTR.Core.DTR.DisplayRule.ColumnsViewRule.DisplayRules;

internal class SpecialHolidayDisplayRule : IColumnDisplayRule
{
    public SpecialHolidayDisplayRule()
    {
    }
    public TimeRange ApplyRules(DisplayContext context)
    {
        if (context.TimeContext.IsRestDay() || context.TimeContext.IsSpecialWorking())
        {
            return TimeRange.Empty;
        }
        return context.PipeLineResult.SpecialHoliday;
    }
}