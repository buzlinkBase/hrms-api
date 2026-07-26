using DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;

namespace DTR.Core.DTR.DisplayRule.ColumnsViewRule.DisplayRules;

internal class HolidayToRegularRule : IColumnDisplayRule
{
    public HolidayToRegularRule()
    {
    }

    public TimeRange ApplyRules(DisplayContext context)
    {
        var regular = context.PipeLineResult.Regular;
        var holiday = context.PipeLineResult.LegalHoliday;
        var IsHolPlusReg= new IsShowWorkOnHolidayInRegColumn().IsSatisfiedBy(context);
        if (IsHolPlusReg)
        {
            return regular + holiday;
        }
        if (context.TimeContext.IsSpecialWorking())
        {
            return context.PipeLineResult.SpecialHoliday;
        }
        return regular;
    }
}
