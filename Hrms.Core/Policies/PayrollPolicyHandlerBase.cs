namespace Hrms.Core.Policies;

public interface IPayrollPolicyHandler
{
    IPayrollPolicyHandler SetNext(IPayrollPolicyHandler next);
    BasicPipelineData Handle(BasicPipelineData line, PayrollContext context);
}

public abstract class PayrollPolicyHandlerBase : IPayrollPolicyHandler
{
    private IPayrollPolicyHandler _next;

    public IPayrollPolicyHandler SetNext(IPayrollPolicyHandler next)
    {
        _next = next;
        return next;
    }

    public BasicPipelineData Handle(BasicPipelineData line, PayrollContext context)
    {
        if (CanHandle(context))
        {
            return Apply(line, context);
        }

        return _next?.Handle(line, context) ?? line;
    }

    protected abstract bool CanHandle(PayrollContext context);
    protected abstract BasicPipelineData Apply(BasicPipelineData line, PayrollContext context);
}