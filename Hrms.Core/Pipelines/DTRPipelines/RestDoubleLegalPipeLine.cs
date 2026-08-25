namespace Hrms.Core.Pipelines;

public class RestDoubleLegalPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RestDoubleLegalPolicy());
    }
}
