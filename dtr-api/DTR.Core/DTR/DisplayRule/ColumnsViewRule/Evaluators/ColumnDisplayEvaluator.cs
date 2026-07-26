namespace DTR.Core.DTR.DisplayRule.ColumnsViewRule.Evaluators;

public interface IColumnDisplayRule
{
    TimeRange ApplyRules(DisplayContext context);
}
public class ColumnDisplayEvaluator
{
    private readonly IColumnDisplayRule _strategy;
    public ColumnDisplayEvaluator(IColumnDisplayRule strategy)
    {
        _strategy = strategy;
    }
    public TimeRange Handle(DisplayContext context)
    {
        return _strategy.ApplyRules(context);
    }
}