namespace Hrms.Core.Pipelines;

public class RestSpecialDayPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RestSpecialDayPolicy());
    }
}
