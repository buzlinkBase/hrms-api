namespace Hrms.Core.Pipelines;

public class OvertimePipeline : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new OvertimePolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
