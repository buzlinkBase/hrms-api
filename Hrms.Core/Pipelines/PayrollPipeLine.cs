
namespace Hrms.Core.Pipelines;

public class PayrollPipeLine<Context, Payload>
    where Context : IPayloadContext
    where Payload : IPipeData, new()
{
    private List<PayrollPolicyBase<Payload, Context>> policies;
    public PayrollPipeLine()
    {
        policies = new List<PayrollPolicyBase<Payload, Context>>();
    }

    public PayrollPipeLine<Context, Payload> AddPolicy(PayrollPolicyBase<Payload, Context> payrollPolicy)
    {
        policies.Add(payrollPolicy);
        return this;
    }

    public Payload Execute(Payload line, Context context)
    {
        foreach (var policy in policies)
        {
            line = policy.Apply(line, context);
        }
        return line;
    }
}
