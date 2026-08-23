namespace Hrms.Core.Pipelines;

public class RestDoubleLegalPipeLine : BasicPipelineBase
{
    public override BasicPipelineData Run(PayrollContext context)
    {
        pipeline
            .AddPolicy(new RestDoubleLegalPolicy())
            ;
        return pipeline.Execute(new BasicPipelineData(), context);
    }
}
