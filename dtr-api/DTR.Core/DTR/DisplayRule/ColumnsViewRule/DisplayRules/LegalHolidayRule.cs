using DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;
namespace DTR.Core.DTR.DisplayRule.ColumnsViewRule.DisplayRules;

internal class LegalHolidayRule : IColumnDisplayRule
{
    public LegalHolidayRule()
    {
    }
    public TimeRange ApplyRules(DisplayContext context)
    {
        var holOption = new IsShowWorkOnHolidayInRegColumn().IsSatisfiedBy(context);
        if (!holOption)
        {
            return context.PipeLineResult.LegalHoliday;
        }
        return TimeRange.Empty;
    }
}

internal class LegalOTDisplay : IColumnDisplayRule
{
    private readonly TimeRange _holOT;

    public LegalOTDisplay(TimeRange holOT)
    {
        _holOT = holOT;
    }
    public TimeRange ApplyRules(DisplayContext context)
    {
        if (!context.PipeLineResult.Plus8.IsDoubleHoliday()) return TimeRange.Empty;
        return _holOT;
    }
}
