namespace Hrms.Core.Pipelines;

public class RestLegalDayPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline
            .AddPolicy(new RestLegalDayPolicy())
            ;
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
