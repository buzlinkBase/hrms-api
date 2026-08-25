namespace Hrms.Core.Pipelines;

public class LeavePipeline : BasicPipelineCollectionBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, LineCollection<BasicPipelineData>> pipeline)
    {
        pipeline.AddPolicy(new LeavePolicy());
    }
}



