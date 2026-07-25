namespace DTR.Core;

internal class SPColumEvaluator : IColumnEvaluator
{
    public SPColumEvaluator()
    {
    }
    public TimeRange ApplyRules(DisplayContext context)
    {
        if (context.TimeContext.IsRestDay()) return TimeRange.Empty;
        return context.PipeLineResult.SpecialHoliday;
    }
}