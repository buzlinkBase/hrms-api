
namespace Hrms.Core.Policies;

public abstract class PayrollPolicyBase<Payload, Context>
    where Payload : IPipeData, new()
    where Context : IPayloadContext
{
    private readonly IPayrollSpec<Context> _spec;
    private readonly SpecFailBehaviour _specFailBehaviour;
    protected PayrollPolicyBase()
    {
        _spec = new IsTrueSpec<Context>();
        _specFailBehaviour = SpecFailBehaviour.ReturnInput;
    }
    public PayrollPolicyBase(IPayrollSpec<Context> spec, SpecFailBehaviour specFailBehaviour = SpecFailBehaviour.ReturnInput)
    {
        _spec = spec;
        _specFailBehaviour = specFailBehaviour;
    }

    public Payload Apply(Payload line, Context context)
    {
        if (_spec.IsSatisfiedBy(context))
            return ApplyIfSatisfied(line, context);

        if (_specFailBehaviour == SpecFailBehaviour.ReturnInput)
            return line;

        return new();
    }
    public abstract Payload ApplyIfSatisfied(Payload line, Context context);
}

public enum SpecFailBehaviour
{
    ReturnNewInstance,
    ReturnInput
}
