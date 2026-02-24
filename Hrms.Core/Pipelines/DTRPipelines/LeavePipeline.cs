namespace Hrms.Core.Pipelines;

public class LeavePipeline : BasicPipelineCollectionBase
{
    public override LineCollection<BasicPipelineData> Run(PayrollContext context)
    {
        pipeline.AddPolicy(new LeavePolicy());
        return pipeline.Execute(new LineCollection<BasicPipelineData>(), context);
    }
}


