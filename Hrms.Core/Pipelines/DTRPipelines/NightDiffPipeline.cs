namespace Hrms.Core.Pipelines;

public class NightDiffPipeline : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new NightDiffPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
