namespace Hrms.Core.Pipelines;

public class RestDayPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RestDayPolicy());
    }
}
