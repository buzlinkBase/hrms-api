namespace Hrms.Core.Pipelines;

public class RestLegalDayPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RestLegalDayPolicy());
    }
}
