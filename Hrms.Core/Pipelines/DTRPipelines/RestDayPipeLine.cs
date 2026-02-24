namespace Hrms.Core.Pipelines;

public class RestDayPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline.AddPolicy(new RestDayPolicy());
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
